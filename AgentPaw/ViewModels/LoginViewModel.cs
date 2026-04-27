using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private const int LoginTimeoutSeconds = 30;

    private readonly AuthService _authService;
    private readonly MainViewModel _mainViewModel;
    private CancellationTokenSource? _loginCts;
    private TcpListener? _activeListener;
    private System.Windows.Threading.DispatcherTimer? _countdownTimer;
    private DateTime _loginDeadlineUtc;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _remainingSeconds;

    [ObservableProperty]
    private string? _errorMessage;

    public event Action? LoginSucceeded;

    public LoginViewModel(AuthService authService, MainViewModel mainViewModel)
    {
        _authService = authService;
        _mainViewModel = mainViewModel;
    }

    [RelayCommand]
    private async Task LoginWithGoogleAsync()
    {
        // 직전 로그인 시도가 아직 살아 있으면 먼저 정리한다
        try { _loginCts?.Cancel(); } catch { /* ignore */ }
        _loginCts = new CancellationTokenSource();
        var ct = _loginCts.Token;

        IsLoading = true;
        ErrorMessage = null;
        StartCountdown();

        TcpListener? tcpListener = null;
        try
        {
            var loginUrl = _authService.GetLoginUrl();
            var port = _authService.GetRedirectPort();

            tcpListener = new TcpListener(IPAddress.Loopback, port);
            tcpListener.Start();
            _activeListener = tcpListener;

            // Open browser for Google OAuth
            Process.Start(new ProcessStartInfo(loginUrl) { UseShellExecute = true });

            // 취소 요청 시 TcpListener를 멈춰 AcceptTcpClientAsync가 즉시 예외로 깨어나도록 연결
            using var reg = ct.Register(() =>
            {
                try { tcpListener?.Stop(); } catch { /* ignore */ }
            });

            // 브라우저가 favicon 등 여러 요청을 보낼 수 있으므로,
            // code 파라미터가 포함된 요청을 찾을 때까지 반복한다.
            string? code = null;
            var timeout = Task.Delay(TimeSpan.FromSeconds(LoginTimeoutSeconds), ct);

            while (code == null)
            {
                ct.ThrowIfCancellationRequested();

                var acceptTask = tcpListener.AcceptTcpClientAsync();
                var completed = await Task.WhenAny(acceptTask, timeout);

                if (ct.IsCancellationRequested) return;

                if (completed == timeout)
                {
                    ErrorMessage = "로그인 시간이 초과되었습니다.";
                    return;
                }

                using var client = await acceptTask;
                await using var stream = client.GetStream();

                // Read the full HTTP request
                var buffer = new byte[4096];
                var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var requestLine = request.Split('\n')[0];

                // Extract code parameter
                var codeMatch = Regex.Match(requestLine, @"[?&]code=([^&\s]+)");
                if (codeMatch.Success)
                {
                    code = Uri.UnescapeDataString(codeMatch.Groups[1].Value);
                }

                // Always send a response to close the browser tab
                string body;
                if (code != null)
                {
                    body = "<html><body style='font-family:sans-serif;text-align:center;padding:60px'>"
                         + "<h2>Agent Paw</h2>"
                         + "<p>로그인 완료. 이 창을 닫아도 됩니다.</p>"
                         + "<script>window.close()</script></body></html>";
                }
                else
                {
                    body = "<html><body></body></html>";
                }

                var bodyBytes = Encoding.UTF8.GetBytes(body);
                var responseHeader = $"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
                var headerBytes = Encoding.UTF8.GetBytes(responseHeader);

                await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
                await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length);
                await stream.FlushAsync();
            }

            tcpListener.Stop();
            tcpListener = null;

            var (jwt, user) = await _authService.ExchangeCodeAndLoginAsync(code);

            _mainViewModel.SetAuthenticated(new SessionInfo
            {
                UserId = user.UserId,
                Email = user.Email,
                DisplayName = user.DisplayName,
                ProfileImageUrl = user.ProfileImageUrl
            });

            LoginSucceeded?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // 사용자가 취소한 경우 — 에러 메시지 없이 조용히 리셋
        }
        catch (ObjectDisposedException)
        {
            // 취소로 인해 listener가 dispose되어 accept가 깨어난 케이스 — 동일하게 취급
        }
        catch (SocketException) when (_loginCts?.IsCancellationRequested == true)
        {
            // 취소 시점에 Socket이 끊겨 발생한 경우 — 동일하게 취급
        }
        catch (SocketException)
        {
            ErrorMessage = $"콜백 수신 포트({_authService.GetRedirectPort()})가 사용 중입니다. 해당 포트를 점유한 프로세스를 종료 후 다시 시도하세요.";
        }
        catch (InvalidOperationException ex) when (ex.Message == "ACCOUNT_INACTIVE")
        {
            ErrorMessage = "비활성화된 계정입니다. 관리자에게 문의하세요.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"로그인 실패: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            try { tcpListener?.Stop(); } catch { /* ignore */ }
            _activeListener = null;
            _loginCts?.Dispose();
            _loginCts = null;
            StopCountdown();
            IsLoading = false;
        }
    }

#if DEBUG || DEVBYPASS
    public bool IsDevBypassAvailable => true;

    [RelayCommand]
    private async Task DevBypassLoginAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var (_, user) = await _authService.DevBypassLoginAsync();
            _mainViewModel.SetAuthenticated(new SessionInfo
            {
                UserId = user.UserId,
                Email = user.Email,
                DisplayName = user.DisplayName,
                ProfileImageUrl = user.ProfileImageUrl
            });
            LoginSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message ?? string.Empty;
            ErrorMessage = $"Dev bypass 실패: {ex.Message}" + (inner.Length > 0 ? $"\n{inner}" : string.Empty);
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agentpaw_devbypass_error.txt");
            System.IO.File.WriteAllText(logPath, $"{ex}\n\nInner:\n{ex.InnerException}");
        }
        finally
        {
            IsLoading = false;
        }
    }
#endif // DEBUG || DEVBYPASS

    [RelayCommand]
    private void CancelLogin()
    {
        if (!IsLoading) return;
        try { _loginCts?.Cancel(); } catch { /* ignore */ }
        try { _activeListener?.Stop(); } catch { /* ignore */ }
        // IsLoading·타이머는 LoginWithGoogleAsync의 finally에서 리셋된다
    }

    private void StartCountdown()
    {
        StopCountdown();
        _loginDeadlineUtc = DateTime.UtcNow.AddSeconds(LoginTimeoutSeconds);
        RemainingSeconds = LoginTimeoutSeconds;
        _countdownTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _countdownTimer.Tick += (_, _) =>
        {
            var left = (int)Math.Max(0, Math.Ceiling((_loginDeadlineUtc - DateTime.UtcNow).TotalSeconds));
            RemainingSeconds = left;
            if (left <= 0) StopCountdown();
        };
        _countdownTimer.Start();
    }

    private void StopCountdown()
    {
        if (_countdownTimer == null) return;
        _countdownTimer.Stop();
        _countdownTimer = null;
    }
}
