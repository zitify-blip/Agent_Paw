using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public class DevProjectRecord
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public DateTimeOffset LastModified { get; init; }

    public string RelativeTime
    {
        get
        {
            var diff = DateTimeOffset.Now - LastModified;
            if (diff.TotalMinutes < 1) return "방금";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}분 전";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}시간 전";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}일 전";
            return LastModified.ToString("M/d");
        }
    }
}

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
    [ObservableProperty] private bool _isDevRunning;
    [ObservableProperty] private bool _isGitDeploying;
    [ObservableProperty] private string _gitRemoteUrl = "";

    // 현재 연결된 AgentPaw 프로젝트 이름 (헤더 표시용)
    [ObservableProperty] private string _linkedProjectName = "";

    // 개발 프로젝트 저장 루트 (baseRoot/{projectId}/)
    [ObservableProperty] private string _devProjectsRoot = "";

    // 현재 선택된 기존 프로젝트 (null = 새 프로젝트)
    [ObservableProperty] private DevProjectRecord? _selectedProject;

    private string _baseDevRoot = DevAgentService.LoadSavedRoot();
    private string _currentProjectId = "";
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _devRunCts;
    private bool _hasSession;
    private string _accumulated = "";

    public ObservableCollection<DevAgentMessage> Messages { get; } = [];
    public ObservableCollection<DevProjectRecord> DevProjects { get; } = [];

    public DevAgentViewModel(DevAgentService devAgent)
    {
        _devAgent = devAgent;
        // 프로젝트가 없을 때 기본 루트
        DevProjectsRoot = _baseDevRoot;
    }

    /// <summary>
    /// MainWindow에서 현재 AgentPaw 프로젝트를 연결한다.
    /// 개발 루트를 baseRoot/{projectId}/ 로 자동 설정한다.
    /// </summary>
    public void SetProject(string projectId, string projectName)
    {
        LinkedProjectName = projectName;

        if (_currentProjectId == projectId) return; // 동일 프로젝트면 세션 유지

        _currentProjectId = projectId;
        DevProjectsRoot = string.IsNullOrEmpty(projectId)
            ? _baseDevRoot
            : Path.Combine(_baseDevRoot, projectId);

        // 프로젝트가 바뀌면 선택·세션 초기화
        SelectedProject = null;
        _hasSession = false;
        Messages.Clear();
        RefreshProjectList();
    }

    // ─────────────────────────────────────────────────────────
    // 프로젝트 목록

    private void RefreshProjectList()
    {
        DevProjects.Clear();
        if (!Directory.Exists(DevProjectsRoot)) return;
        var dirs = Directory.GetDirectories(DevProjectsRoot);
        var records = dirs
            .Select(dir =>
            {
                try
                {
                    return new DevProjectRecord
                    {
                        Name = System.IO.Path.GetFileName(dir),
                        Path = dir,
                        LastModified = new DateTimeOffset(Directory.GetLastWriteTime(dir))
                    };
                }
                catch { return null; }
            })
            .Where(r => r != null)
            .OrderByDescending(r => r!.LastModified)
            .ToList();

        foreach (var r in records)
            DevProjects.Add(r!);
    }

    [RelayCommand]
    private void SelectProject(DevProjectRecord? project)
    {
        if (SelectedProject?.Path == project?.Path) return;

        SelectedProject = project;
        _hasSession = false;
        Messages.Clear();
        _ = LoadGitRemoteAsync(project);
    }

    private async Task LoadGitRemoteAsync(DevProjectRecord? project)
    {
        GitRemoteUrl = project == null ? "" : (await DevAgentService.GetGitRemoteAsync(project.Path) ?? "");
    }

    [RelayCommand]
    private void NewProject()
    {
        SelectedProject = null;
        _hasSession = false;
        Messages.Clear();
    }

    [RelayCommand]
    private void OpenInExplorer()
    {
        var path = SelectedProject?.Path ?? DevProjectsRoot;
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        try { Process.Start("explorer.exe", path); } catch { }
    }

    [RelayCommand]
    private void OpenInVsCode()
    {
        var path = SelectedProject?.Path ?? DevProjectsRoot;
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        try { Process.Start(new ProcessStartInfo("code", $"\"{path}\"") { UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void ChangeDevRoot()
    {
        // WPF-native 폴더 선택: SaveFileDialog trick
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "개발 프로젝트 저장 폴더 선택 — 원하는 폴더로 이동 후 저장 클릭",
            FileName = "여기를 클릭",
            Filter = "폴더 선택|*.folder",
            CheckPathExists = false,
            ValidateNames = false,
            OverwritePrompt = false,
            InitialDirectory = DevProjectsRoot
        };
        if (dialog.ShowDialog() != true) return;

        var path = System.IO.Path.GetDirectoryName(dialog.FileName) ?? dialog.FileName;
        if (string.IsNullOrWhiteSpace(path)) return;

        _baseDevRoot = path;
        DevAgentService.SaveRoot(path);
        DevProjectsRoot = string.IsNullOrEmpty(_currentProjectId)
            ? _baseDevRoot
            : Path.Combine(_baseDevRoot, _currentProjectId);

        SelectedProject = null;
        _hasSession = false;
        Messages.Clear();
        RefreshProjectList();
    }

    // ─────────────────────────────────────────────────────────
    // 메시지 전송

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

        _accumulated = "";
        var assistantMsg = new DevAgentMessage { Role = "assistant", Content = "", IsStreaming = true };
        Messages.Add(assistantMsg);

        _cts = new CancellationTokenSource();

        try
        {
            // 선택된 기존 프로젝트 → 그 디렉토리에서 실행
            // 새 프로젝트 요청 → 저장 루트에서 실행 (Claude가 하위 폴더 생성)
            var workDir = SelectedProject?.Path ?? DevProjectsRoot;
            if (!Directory.Exists(workDir)) Directory.CreateDirectory(workDir);

            // 새 세션이고 선택된 프로젝트가 없으면 컨텍스트 주입
            var systemContext = (!_hasSession && SelectedProject == null)
                ? DevAgentService.BuildNewProjectContext(DevProjectsRoot)
                : null;

            var progress = new Progress<DevStreamEvent>(evt =>
                Application.Current.Dispatcher.Invoke(() => HandleEvent(evt, assistantMsg)));

            await _devAgent.StreamAsync(prompt, workDir, _hasSession, systemContext, progress, _cts.Token);
            _hasSession = true;
        }
        catch (OperationCanceledException)
        {
            assistantMsg.Content = string.IsNullOrEmpty(assistantMsg.Content)
                ? "*[취소됨]*"
                : assistantMsg.Content + "\n\n*[취소됨]*";
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

            // 실행 후 프로젝트 목록 갱신 (새 폴더 생성 반영)
            RefreshProjectList();

            // 새 프로젝트로 생성된 첫 번째 폴더를 자동 선택
            if (SelectedProject == null && DevProjects.Count > 0)
                SelectedProject = DevProjects[0];
        }
    }

    private void HandleEvent(DevStreamEvent evt, DevAgentMessage msg)
    {
        if (evt.Type == "assistant" && evt.Text != null)
        {
            // 턴마다 새 텍스트가 오면 누적 — 이전 턴 내용이 덮어써지지 않도록
            _accumulated = string.IsNullOrEmpty(_accumulated)
                ? evt.Text
                : _accumulated + "\n\n" + evt.Text;
            msg.Content = _accumulated;
        }
        else if (evt.Type == "tool_done")
        {
            // 도구 실행 완료 시 파일 트리 즉시 갱신 (생성된 파일 즉시 반영)
            RefreshProjectList();
        }
    }

    private bool CanSend() => !IsRunning && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel()
    {
        _cts?.Cancel();
        _devAgent.Cancel();
    }

    // ─────────────────────────────────────────────────────────
    // 로컬 실행

    [RelayCommand(CanExecute = nameof(CanRunDev))]
    private async Task RunDevAsync()
    {
        if (SelectedProject == null) return;
        _devRunCts = new CancellationTokenSource();
        IsDevRunning = true;
        RunDevCommand.NotifyCanExecuteChanged();

        var msg = new DevAgentMessage { Role = "assistant", Content = "", IsStreaming = true };
        Messages.Add(msg);

        var progress = new Progress<string>(text =>
            Application.Current.Dispatcher.Invoke(() => msg.Content += text));

        try
        {
            await _devAgent.RunProjectAsync(SelectedProject.Path, progress, _devRunCts.Token);
        }
        catch (OperationCanceledException)
        {
            msg.Content += "\n▶ 중지됨";
        }
        catch (Exception ex)
        {
            msg.Content += $"\n[오류] {ex.Message}";
        }
        finally
        {
            msg.IsStreaming = false;
            IsDevRunning = false;
            _devRunCts?.Dispose();
            _devRunCts = null;
            RunDevCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanRunDev() => SelectedProject != null && !IsDevRunning;

    [RelayCommand]
    private void StopDev()
    {
        _devRunCts?.Cancel();
        _devAgent.StopRunningProject();
    }

    // ─────────────────────────────────────────────────────────
    // GitHub 배포

    [RelayCommand(CanExecute = nameof(CanDeployToGit))]
    private async Task DeployToGitAsync()
    {
        if (SelectedProject == null) return;
        IsGitDeploying = true;
        DeployToGitCommand.NotifyCanExecuteChanged();

        var msg = new DevAgentMessage { Role = "assistant", Content = "", IsStreaming = true };
        Messages.Add(msg);

        var remoteUrl = GitRemoteUrl.Trim();
        var progress = new Progress<string>(text =>
            Application.Current.Dispatcher.Invoke(() => msg.Content += text));

        try
        {
            await _devAgent.GitPushAsync(
                SelectedProject.Path,
                string.IsNullOrEmpty(remoteUrl) ? null : remoteUrl,
                progress,
                CancellationToken.None);

            // 배포 성공 후 remote URL 재확인하여 갱신
            if (!string.IsNullOrEmpty(remoteUrl))
            {
                var loaded = await DevAgentService.GetGitRemoteAsync(SelectedProject.Path);
                GitRemoteUrl = loaded ?? remoteUrl;
            }
        }
        catch (Exception ex)
        {
            msg.Content += $"\n[오류] {ex.Message}";
        }
        finally
        {
            msg.IsStreaming = false;
            IsGitDeploying = false;
            DeployToGitCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanDeployToGit() => SelectedProject != null && !IsGitDeploying;
}
