using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AgentPaw.Services;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class DashboardPage : UserControl
{
    public event Action<string, string>? ProjectSelected;

    public DashboardPage()
    {
        InitializeComponent();
    }

    private async void DashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            UpdateDialogTitle(vm);
            await vm.LoadProjectsCommand.ExecuteAsync(null);
        }
    }

    private void CreateProject_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
        {
            vm.OpenCreateDialogCommand.Execute(null);
            UpdateDialogTitle(vm);
            ProjectNameInput.Focus();
        }
    }

    private void EditProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ProjectListItem project } && DataContext is DashboardViewModel vm)
        {
            vm.OpenEditDialogCommand.Execute(project);
            UpdateDialogTitle(vm);
            ProjectNameInput.Focus();
        }
    }

    private async void ArchiveProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ProjectListItem project } && DataContext is DashboardViewModel vm)
        {
            var result = MessageBox.Show(
                $"'{project.ProjectName}' 프로젝트를 보관하시겠습니까?",
                "프로젝트 보관", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
                await vm.ArchiveProjectCommand.ExecuteAsync(project);
        }
    }

    private async void RestoreProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ProjectListItem project } && DataContext is DashboardViewModel vm)
        {
            await vm.RestoreProjectCommand.ExecuteAsync(project);
        }
    }

    private async void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ProjectListItem project } && DataContext is DashboardViewModel vm)
        {
            var result = MessageBox.Show(
                $"'{project.ProjectName}' 프로젝트를 영구 삭제하시겠습니까?\n이 작업은 되돌릴 수 없습니다.",
                "프로젝트 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                await vm.DeleteProjectCommand.ExecuteAsync(project);
        }
    }

    private void ToggleArchived_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            vm.ToggleArchivedCommand.Execute(null);
    }

    private void ProjectCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ProjectListItem project })
        {
            ProjectSelected?.Invoke(project.ProjectId, project.ProjectName);
        }
    }

    private async void DialogSave_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            await vm.SaveProjectCommand.ExecuteAsync(null);
    }

    private void DialogCancel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            vm.CloseDialogCommand.Execute(null);
    }

    private void DialogOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            vm.CloseDialogCommand.Execute(null);
    }

    private void DialogContent_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true; // Prevent closing when clicking dialog content
    }

    private void Dialog_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape
            && DataContext is DashboardViewModel vm
            && vm.IsDialogOpen)
        {
            vm.CloseDialogCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void UpdateDialogTitle(DashboardViewModel vm)
    {
        DialogTitle.Text = vm.IsEditMode ? "프로젝트 편집" : "새 프로젝트";
    }
}
