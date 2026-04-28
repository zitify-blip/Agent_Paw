using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class WikiPage : UserControl
{
    public WikiPage()
    {
        InitializeComponent();
    }

    public void Initialize(WikiViewModel vm)
    {
        DataContext = vm;
    }

    private void SearchInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is WikiViewModel vm)
        {
            e.Handled = true;
            _ = vm.SearchCommand.ExecuteAsync(null);
        }
    }

    private void TreeNode_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WikiNode node } && DataContext is WikiViewModel vm)
            vm.SelectNodeCommand.Execute(node);
    }

    private void ChevronToggle_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { Tag: WikiNode node } && DataContext is WikiViewModel vm)
            vm.ToggleExpandCommand.Execute(node);
    }

    private void CreateRoot_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm)
        {
            vm.OpenCreateRootDialogCommand.Execute(null);
            NewTitleInput.Focus();
        }
    }

    private void CreateChild_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm)
        {
            vm.OpenCreateChildDialogCommand.Execute(null);
            NewTitleInput.Focus();
        }
    }

    private void CloseCreateDialog_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm) vm.CloseCreateDialogCommand.Execute(null);
    }

    private async void SaveWiki_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm) await vm.CreateWikiCommand.ExecuteAsync(null);
    }

    private void StartEdit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm)
        {
            vm.StartEditCommand.Execute(null);
            EditContentBox.Focus();
        }
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm) vm.CancelEditCommand.Execute(null);
    }

    private async void SaveEdit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm) await vm.SaveEditCommand.ExecuteAsync(null);
    }

    private async void DeleteNode_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WikiViewModel vm || vm.SelectedNode == null) return;
        var hasChildren = vm.SelectedNode.HasChildren;
        var msg = hasChildren
            ? $"'{vm.SelectedNode.Title}' 페이지와 모든 하위 페이지를 삭제하겠습니까?\n이 작업은 되돌릴 수 없다."
            : $"'{vm.SelectedNode.Title}' 페이지를 삭제하겠습니까?";
        var result = MessageBox.Show(msg, "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
            await vm.DeleteNodeCommand.ExecuteAsync(null);
    }

    private async void ConsolidateWiki_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm)
            await vm.ConsolidateWikiCommand.ExecuteAsync(null);
    }

    private void DialogOverlay_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WikiViewModel vm && vm.IsCreateDialogOpen)
            vm.CloseCreateDialogCommand.Execute(null);
    }

    private void DialogContent_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (DataContext is not WikiViewModel vm) return;

        if (vm.IsCreateDialogOpen)
        {
            vm.CloseCreateDialogCommand.Execute(null);
            e.Handled = true;
        }
        else if (vm.IsEditing)
        {
            vm.CancelEditCommand.Execute(null);
            e.Handled = true;
        }
    }
}
