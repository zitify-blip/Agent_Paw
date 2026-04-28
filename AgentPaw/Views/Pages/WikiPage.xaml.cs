using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AgentPaw.Models;
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

    private void FilterAll_Click(object sender, RoutedEventArgs e)
    {
        SetFilterAppearance("ALL");
        if (DataContext is WikiViewModel vm) _ = vm.SetCategoryFilterCommand.ExecuteAsync("ALL");
    }

    private void FilterAdr_Click(object sender, RoutedEventArgs e)
    {
        SetFilterAppearance("WIKI_ADR");
        if (DataContext is WikiViewModel vm) _ = vm.SetCategoryFilterCommand.ExecuteAsync("WIKI_ADR");
    }

    private void FilterSpec_Click(object sender, RoutedEventArgs e)
    {
        SetFilterAppearance("WIKI_SPEC");
        if (DataContext is WikiViewModel vm) _ = vm.SetCategoryFilterCommand.ExecuteAsync("WIKI_SPEC");
    }

    private void FilterTrouble_Click(object sender, RoutedEventArgs e)
    {
        SetFilterAppearance("WIKI_TROUBLE");
        if (DataContext is WikiViewModel vm) _ = vm.SetCategoryFilterCommand.ExecuteAsync("WIKI_TROUBLE");
    }

    private void SetFilterAppearance(string active)
    {
        AllFilterBtn.Appearance = active == "ALL" ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
        AdrFilterBtn.Appearance = active == "WIKI_ADR" ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
        SpecFilterBtn.Appearance = active == "WIKI_SPEC" ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
        TroubleFilterBtn.Appearance = active == "WIKI_TROUBLE" ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
    }

    private void WikiItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: WikiDocument wiki } && DataContext is WikiViewModel vm)
            _ = vm.SelectWikiCommand.ExecuteAsync(wiki);
    }

    private void BackToList_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm) vm.BackToListCommand.Execute(null);
    }

    private void CreateWiki_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm)
        {
            vm.OpenCreateDialogCommand.Execute(null);
            NewTitleInput.Focus();
        }
    }

    private async void ConsolidateWiki_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm)
            await vm.ConsolidateWikiCommand.ExecuteAsync(null);
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
        if (DataContext is WikiViewModel vm) vm.StartEditCommand.Execute(null);
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm) vm.CancelEditCommand.Execute(null);
    }

    private async void SaveEdit_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WikiViewModel vm) await vm.SaveEditCommand.ExecuteAsync(null);
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
        else if (vm.IsDetailView)
        {
            vm.BackToListCommand.Execute(null);
            e.Handled = true;
        }
    }
}
