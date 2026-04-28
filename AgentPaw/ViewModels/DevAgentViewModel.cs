using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public partial class DevAgentMessage : ObservableObject
{
    [ObservableProperty] private string _content = "";
    [ObservableProperty] private bool _isStreaming;

    public string Role { get; init; } = "user";
    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
}

public partial class DevAgentViewModel : ObservableObject
{
    private readonly DevAgentService _devAgent;

    [ObservableProperty] private string _inputText = "";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _projectName = "";
    [ObservableProperty] private string _workingDirectory = "";

    private CancellationTokenSource? _cts;
    private bool _hasSession;
    private string _lastProjectDir = "";

    public ObservableCollection<DevAgentMessage> Messages { get; } = [];

    public DevAgentViewModel(DevAgentService devAgent)
    {
        _devAgent = devAgent;
    }

    public void SetProject(string name, string path)
    {
        ProjectName = name;
        WorkingDirectory = path;

        // 프로젝트 디렉토리가 바뀌면 세션 초기화
        if (_lastProjectDir != path)
        {
            _hasSession = false;
            _lastProjectDir = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var prompt = InputText.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        InputText = "";
        IsRunning = true;
        SendCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();

        Messages.Add(new DevAgentMessage { Role = "user", Content = prompt });

        var assistantMsg = new DevAgentMessage { Role = "assistant", Content = "", IsStreaming = true };
        Messages.Add(assistantMsg);

        _cts = new CancellationTokenSource();

        try
        {
            var dir = string.IsNullOrEmpty(WorkingDirectory)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : WorkingDirectory;

            var progress = new Progress<DevStreamEvent>(evt =>
                Application.Current.Dispatcher.Invoke(() => HandleEvent(evt, assistantMsg)));

            await _devAgent.StreamAsync(prompt, dir, _hasSession, progress, _cts.Token);
            _hasSession = true;
        }
        catch (OperationCanceledException)
        {
            if (string.IsNullOrEmpty(assistantMsg.Content))
                assistantMsg.Content = "*[취소됨]*";
            else
                assistantMsg.Content += "\n\n*[취소됨]*";
        }
        catch (Exception ex)
        {
            assistantMsg.Content = $"*[오류]* {ex.Message}";
        }
        finally
        {
            assistantMsg.IsStreaming = false;
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
            SendCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    private static void HandleEvent(DevStreamEvent evt, DevAgentMessage msg)
    {
        if (evt.Type == "assistant" && evt.Text != null)
            msg.Content = evt.Text;
    }

    private bool CanSend() => !IsRunning && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel()
    {
        _cts?.Cancel();
        _devAgent.Cancel();
    }
}
