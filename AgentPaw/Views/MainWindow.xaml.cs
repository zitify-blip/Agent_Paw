using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using AgentPaw.Services;
using AgentPaw.ViewModels;
using AgentPaw.Views.Pages;

namespace AgentPaw.Views;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly AuthService _authService;
    private bool _personaInitialized;
    private bool _instructionsInitialized;
    private bool _settingsInitialized;
    private bool _llmInitialized;
    private bool _integrationInitialized;
    private bool _otherSettingsInitialized;
    private bool _devAgentInitialized;
    private SettingsViewModel? _settingsVm;
    private WorkspaceViewModel? _currentWorkspaceVm;
    private string _currentPage = "projects";

    // ProjectId → ViewModel 캐시 (메뉴 이동 후 복귀 시 상태 보존)
    private readonly Dictionary<string, WorkspaceViewModel>  _workspaceVmCache  = new();
    private readonly Dictionary<string, TimelineViewModel>   _timelineVmCache   = new();
    private readonly Dictionary<string, WikiViewModel>       _wikiVmCache       = new();

    public MainWindow(MainViewModel viewModel, LoginViewModel loginViewModel, DashboardViewModel dashboardViewModel, AuthService authService)
    {
        _viewModel = viewModel;
        _dashboardViewModel = dashboardViewModel;
        _authService = authService;
        InitializeComponent();
        DataContext = viewModel;

        // Wire up pages
        LoginPageControl.DataContext = loginViewModel;
        DashboardPageControl.DataContext = dashboardViewModel;
        DashboardPageControl.ProjectSelected += OnProjectSelected;
        WorkspacePageControl.BackRequested += OnWorkspaceBack;
        WorkspacePageControl.DevAgentRequested += NavigateToDevAgentForCurrentProject;
        loginViewModel.LoginSucceeded += OnLoginSucceeded;

        // Listen for auth state changes
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.IsAuthenticated))
                UpdateVisibility();
        };

        // 사이드바 버전 표시
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : string.Empty;

        // 저장된 세션 복원 시도
        Loaded += async (_, _) => await TryRestoreSessionAsync();
    }

    private async Task TryRestoreSessionAsync()
    {
        var session = await _authService.TryRestoreSessionAsync();
        if (session != null)
        {
            _viewModel.SetAuthenticated(session);
            UpdateVisibility();
        }
    }

    private void OnLoginSucceeded()
    {
        Dispatcher.Invoke(UpdateVisibility);
    }

    private async void UpdateVisibility()
    {
        if (_viewModel.IsAuthenticated)
        {
            LoginPageControl.Visibility = Visibility.Collapsed;
            AppShell.Visibility = Visibility.Visible;
            UserDisplayName.Text = _viewModel.DisplayName;
            UserEmail.Text = _viewModel.Email;
            HeaderUserName.Text = _viewModel.DisplayName;

            // 대시보드 프로젝트 로드 (세션 복원 후 userId가 설정된 상태)
            await _dashboardViewModel.LoadProjectsCommand.ExecuteAsync(null);
        }
        else
        {
            LoginPageControl.Visibility = Visibility.Visible;
            AppShell.Visibility = Visibility.Collapsed;
        }
    }

    // === Navigation ===

    private void ShowPage(string page)
    {
        _currentPage = page;
        DashboardPageControl.Visibility = page == "projects" ? Visibility.Visible : Visibility.Collapsed;
        WorkspacePageControl.Visibility = page == "workspace" ? Visibility.Visible : Visibility.Collapsed;
        PersonaPageHost.Visibility = page == "persona" ? Visibility.Visible : Visibility.Collapsed;
        InstructionsPageHost.Visibility = page == "instructions" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPageHost.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
        DevAgentPageHost.Visibility = page == "devagent" ? Visibility.Visible : Visibility.Collapsed;
        UpdateChatInProgressBanner();
    }

    // === 대화 진행 중 배너 ===

    private void SubscribeWorkspaceVm(WorkspaceViewModel vm)
    {
        if (_currentWorkspaceVm != null)
            _currentWorkspaceVm.PropertyChanged -= OnWorkspaceVmPropertyChanged;
        _currentWorkspaceVm = vm;
        _currentWorkspaceVm.PropertyChanged += OnWorkspaceVmPropertyChanged;
    }

    private void OnWorkspaceVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(WorkspaceViewModel.IsLoading):
                    UpdateChatInProgressBanner();
                    DogPanel.SetActive(_currentWorkspaceVm?.IsLoading == true);
                    break;
                case nameof(WorkspaceViewModel.StreamingPersonaId):
                case nameof(WorkspaceViewModel.StreamingPreview):
                    DogPanel.SetSpeaker(
                        _currentWorkspaceVm?.StreamingPersonaId,
                        _currentWorkspaceVm?.StreamingPreview);
                    break;
            }
        });
    }

    private void UpdateChatInProgressBanner()
    {
        var show = _currentWorkspaceVm?.IsLoading == true && _currentPage != "workspace";
        ChatInProgressBanner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ChatInProgressProjectName.Text = _currentWorkspaceVm?.ProjectName ?? string.Empty;
    }

    private void ChatInProgressBanner_Click(object sender, MouseButtonEventArgs e)
    {
        ShowPage("workspace");
    }

    private void NavProjects_Click(object sender, RoutedEventArgs e)
    {
        ShowPage("projects");
    }

    private void NavPersona_Click(object sender, RoutedEventArgs e)
    {
        if (!_personaInitialized)
        {
            var vm = App.GetService<ProjectSettingsViewModel>();
            var projectService = App.GetService<ProjectService>();
            var authService = App.GetService<AuthService>();
            var page = new PersonaPage();
            page.Initialize(vm, projectService, authService);
            PersonaPageHost.Content = page;
            _personaInitialized = true;
        }
        ShowPage("persona");
    }

    private void NavInstructions_Click(object sender, RoutedEventArgs e)
    {
        if (!_instructionsInitialized)
        {
            var vm = App.GetService<InstructionsViewModel>();
            var page = new InstructionsPage();
            page.Initialize(vm);
            InstructionsPageHost.Content = page;
            _instructionsInitialized = true;
        }
        ShowPage("instructions");
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!_settingsInitialized)
        {
            _settingsVm ??= App.GetService<SettingsViewModel>();
            _settingsPage = new SettingsPage();
            _settingsPage.Initialize(_settingsVm);
            _settingsInitialized = true;
        }
        SettingsPageHost.Content = _settingsPage;
        ShowPage("settings");
    }

    private void NavLlm_Click(object sender, RoutedEventArgs e)
    {
        if (!_llmInitialized)
        {
            _settingsVm ??= App.GetService<SettingsViewModel>();
            _llmPage = new LlmSettingsPage();
            _llmPage.Initialize(_settingsVm);
            _llmInitialized = true;
        }
        SettingsPageHost.Content = _llmPage;
        ShowPage("settings");
    }

    private void NavApiIntegration_Click(object sender, RoutedEventArgs e)
    {
        if (!_integrationInitialized)
        {
            _settingsVm ??= App.GetService<SettingsViewModel>();
            _integrationPage = new IntegrationSettingsPage();
            _integrationPage.Initialize(_settingsVm);
            _integrationInitialized = true;
        }
        SettingsPageHost.Content = _integrationPage;
        ShowPage("settings");
    }

    private void NavOtherSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!_otherSettingsInitialized)
        {
            _settingsVm ??= App.GetService<SettingsViewModel>();
            _otherSettingsPage = new OtherSettingsPage();
            _otherSettingsPage.Initialize(_settingsVm);
            _otherSettingsInitialized = true;
        }
        SettingsPageHost.Content = _otherSettingsPage;
        ShowPage("settings");
    }

    // Page instance caches
    private SettingsPage? _settingsPage;
    private LlmSettingsPage? _llmPage;
    private IntegrationSettingsPage? _integrationPage;
    private OtherSettingsPage? _otherSettingsPage;
    private DevAgentPage? _devAgentPage;

    private void NavDevAgent_Click(object sender, RoutedEventArgs e)
    {
        NavigateToDevAgentForCurrentProject();
    }

    // WorkspacePage의 숏컷 버튼 또는 네비 버튼에서 호출된다
    internal void NavigateToDevAgentForCurrentProject()
    {
        if (!_devAgentInitialized)
        {
            var vm = App.GetService<DevAgentViewModel>();
            _devAgentPage = new DevAgentPage();
            _devAgentPage.Initialize(vm);
            DevAgentPageHost.Content = _devAgentPage;
            _devAgentInitialized = true;
        }

        // 현재 열린 프로젝트의 ID로 dev 루트 자동 연결
        if (_currentWorkspaceVm != null)
        {
            var vm = App.GetService<DevAgentViewModel>();
            vm.SetProject(
                _currentWorkspaceVm.ProjectId,
                _currentWorkspaceVm.ProjectName ?? "");
        }

        ShowPage("devagent");
    }

    // === Project ===

    private async void OnProjectSelected(string projectId, string projectName)
    {
        // 캐시 히트: 동일 프로젝트 재진입 시 기존 VM 재사용 → 메시지·탭 상태 보존
        var isNew = !_workspaceVmCache.ContainsKey(projectId);

        if (!_workspaceVmCache.TryGetValue(projectId, out var workspaceVm))
        {
            workspaceVm = App.GetService<WorkspaceViewModel>();
            _workspaceVmCache[projectId] = workspaceVm;
        }
        if (!_timelineVmCache.TryGetValue(projectId, out var timelineVm))
        {
            timelineVm = App.GetService<TimelineViewModel>();
            _timelineVmCache[projectId] = timelineVm;
        }
        if (!_wikiVmCache.TryGetValue(projectId, out var wikiVm))
        {
            wikiVm = App.GetService<WikiViewModel>();
            _wikiVmCache[projectId] = wikiVm;
        }

        WorkspacePageControl.Initialize(workspaceVm, resetTab: isNew);
        WorkspacePageControl.SetTimelineViewModel(timelineVm);
        WorkspacePageControl.SetWikiViewModel(wikiVm);

        // ProjectSettings는 항상 최신 상태로 로드
        var projectSettingsVm = App.GetService<ProjectSettingsViewModel>();
        await projectSettingsVm.LoadAsync(projectId, projectName);
        WorkspacePageControl.SetProjectSettingsViewModel(projectSettingsVm);

        SubscribeWorkspaceVm(workspaceVm);

        if (isNew)
        {
            // 첫 진입: DB에서 히스토리 + 페르소나 전체 로드
            await workspaceVm.LoadWorkspaceAsync(projectId, projectName);
            // 타임라인도 미리 로드해 탭 클릭 시 즉시 표시
            await timelineVm.LoadAsync(projectId);
        }

        _dashboardViewModel.ActiveProjectId = projectId;
        ShowPage("workspace");

        // Wire dog animation panel now that personas are loaded
        DogPanel.SetPersonas(workspaceVm.Personas);
        DogPanel.Visibility = Visibility.Visible;
        DogPanel.SetActive(workspaceVm.IsLoading);
    }

    private void OnWorkspaceBack()
    {
        ShowPage("projects");
    }

    private async void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        _workspaceVmCache.Clear();
        _timelineVmCache.Clear();
        _wikiVmCache.Clear();
        DogPanel.Visibility = Visibility.Collapsed;
        DogPanel.SetPersonas([]);
        await _viewModel.LogoutCommand.ExecuteAsync(null);
        UpdateVisibility();
    }
}
