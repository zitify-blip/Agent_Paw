using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AgentPaw.Models;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class TimelinePage : UserControl
{
    public TimelinePage()
    {
        InitializeComponent();
    }

    public void Initialize(TimelineViewModel vm)
    {
        DataContext = vm;
    }

    private void EventsTab_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TimelineViewModel vm)
        {
            vm.SwitchToEventsCommand.Execute(null);
            EventsTabBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
            SnapshotsTabBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
        }
    }

    private void SnapshotsTab_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TimelineViewModel vm)
        {
            vm.SwitchToSnapshotsCommand.Execute(null);
            EventsTabBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
            SnapshotsTabBtn.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
        }
    }

    private void CreateSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TimelineViewModel vm)
        {
            vm.OpenCreateDialogCommand.Execute(null);
            SnapshotDescInput.Focus();
        }
    }

    private void CloseCreateDialog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TimelineViewModel vm)
            vm.CloseCreateDialogCommand.Execute(null);
    }

    private async void SaveSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TimelineViewModel vm)
            await vm.CreateSnapshotCommand.ExecuteAsync(null);
    }

    private async void Rollback_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: Snapshot snapshot } && DataContext is TimelineViewModel vm)
            await vm.OpenRollbackDialogCommand.ExecuteAsync(snapshot);
    }

    private void CloseRollbackDialog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TimelineViewModel vm)
            vm.CloseRollbackDialogCommand.Execute(null);
    }

    private async void ExecuteRollback_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TimelineViewModel vm)
            await vm.ExecuteRollbackCommand.ExecuteAsync(null);
    }

    private void DialogOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is TimelineViewModel vm)
        {
            if (vm.IsCreateDialogOpen) vm.CloseCreateDialogCommand.Execute(null);
            if (vm.IsRollbackDialogOpen) vm.CloseRollbackDialogCommand.Execute(null);
        }
    }

    private void DialogContent_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (DataContext is not TimelineViewModel vm) return;

        if (vm.IsRollbackDialogOpen)
        {
            vm.CloseRollbackDialogCommand.Execute(null);
            e.Handled = true;
        }
        else if (vm.IsCreateDialogOpen)
        {
            vm.CloseCreateDialogCommand.Execute(null);
            e.Handled = true;
        }
    }
}
