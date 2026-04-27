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
    private WorkspaceViewModel? _currentWorkspaceVm;
    private string _currentPage = "projects";

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
        if (e.PropertyName == nameof(WorkspaceViewModel.IsLoading))
            Dispatcher.Invoke(UpdateChatInProgressBanner);
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
            var vm = App.GetService<SettingsViewModel>();
            var page = new SettingsPage();
            page.Initialize(vm);
            SettingsPageHost.Content = page;
            _settingsInitialized = true;
        }
        ShowPage("settings");
    }

    // === Project ===

    private async void OnProjectSelected(string projectId, string projectName)
    {
        var workspaceVm = App.GetService<WorkspaceViewModel>();
        WorkspacePageControl.Initialize(workspaceVm);

        var timelineVm = App.GetService<TimelineViewModel>();
        var wikiVm = App.GetService<WikiViewModel>();
        WorkspacePageControl.SetTimelineViewModel(timelineVm);
        WorkspacePageControl.SetWikiViewModel(wikiVm);

        // ProjectSettings를 WorkspacePage에 연결
        var projectSettingsVm = App.GetService<ProjectSettingsViewModel>();
        await projectSettingsVm.LoadAsync(projectId, projectName);
        WorkspacePageControl.SetProjectSettingsViewModel(projectSettingsVm);

        SubscribeWorkspaceVm(workspaceVm);
        await workspaceVm.LoadWorkspaceAsync(projectId, projectName);

        ShowPage("workspace");
    }

    private void OnWorkspaceBack()
    {
        ShowPage("projects");
    }

    private async void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.LogoutCommand.ExecuteAsync(null);
        UpdateVisibility();
    }
}
