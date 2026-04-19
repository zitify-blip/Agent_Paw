using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly AuthService _authService;
    private readonly ClaudeCliService _cliService;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _showArchived;

    [ObservableProperty]
    private string? _errorMessage;

    // Create/Edit dialog state
    [ObservableProperty]
    private bool _isDialogOpen;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private string _dialogProjectName = string.Empty;

    [ObservableProperty]
    private string _dialogProjectDescription = string.Empty;

    [ObservableProperty]
    private string? _editingProjectId;

    public ObservableCollection<ProjectListItem> Projects { get; } = [];
    public ObservableCollection<ProjectListItem> ArchivedProjects { get; } = [];

    public DashboardViewModel(ProjectService projectService, AuthService authService, ClaudeCliService cliService)
    {
        _projectService = projectService;
        _authService = authService;
        _cliService = cliService;
    }

    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        if (_authService.CurrentUserId == null) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var active = await _projectService.ListProjectsForUserAsync(_authService.CurrentUserId, "ACTIVE");
            Projects.Clear();
            foreach (var p in active)
                Projects.Add(p);

            var archived = await _projectService.ListProjectsForUserAsync(_authService.CurrentUserId, "ARCHIVED");
            ArchivedProjects.Clear();
            foreach (var p in archived)
                ArchivedProjects.Add(p);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"프로젝트 목록 로드 실패: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenCreateDialog()
    {
        IsEditMode = false;
        DialogProjectName = string.Empty;
        DialogProjectDescription = string.Empty;
        EditingProjectId = null;
        IsDialogOpen = true;
    }

    [RelayCommand]
    private void OpenEditDialog(ProjectListItem project)
    {
        IsEditMode = true;
        DialogProjectName = project.ProjectName;
        DialogProjectDescription = project.Description ?? string.Empty;
        EditingProjectId = project.ProjectId;
        IsDialogOpen = true;
    }

    [RelayCommand]
    private void CloseDialog()
    {
        IsDialogOpen = false;
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(DialogProjectName)) return;
        if (_authService.CurrentUserId == null) return;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            if (IsEditMode && EditingProjectId != null)
            {
                await _projectService.UpdateProjectAsync(
                    _authService.CurrentUserId,
                    EditingProjectId,
                    DialogProjectName.Trim(),
                    string.IsNullOrWhiteSpace(DialogProjectDescription) ? null : DialogProjectDescription.Trim());
            }
            else
            {
                await _projectService.CreateProjectAsync(
                    _authService.CurrentUserId,
                    DialogProjectName.Trim(),
                    string.IsNullOrWhiteSpace(DialogProjectDescription) ? null : DialogProjectDescription.Trim());
            }

            IsDialogOpen = false;
            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            ErrorMessage = $"저장 실패: {inner}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ArchiveProjectAsync(ProjectListItem project)
    {
        if (_authService.CurrentUserId == null) return;

        try
        {
            await _projectService.ArchiveProjectAsync(_authService.CurrentUserId, project.ProjectId);
            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"보관 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RestoreProjectAsync(ProjectListItem project)
    {
        if (_authService.CurrentUserId == null) return;

        try
        {
            await _projectService.RestoreProjectAsync(_authService.CurrentUserId, project.ProjectId);
            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"복원 실패: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteProjectAsync(ProjectListItem project)
    {
        if (_authService.CurrentUserId == null) return;

        IsLoading = true;
        ErrorMessage = null;
        try
        {
            // 동작 중인 에이전트 프로세스 모두 종료
            _cliService.KillAll();

            await _projectService.DeleteProjectAsync(_authService.CurrentUserId, project.ProjectId);
            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"삭제 실패: {ex.InnerException?.Message ?? ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleArchived()
    {
        ShowArchived = !ShowArchived;
    }
}
