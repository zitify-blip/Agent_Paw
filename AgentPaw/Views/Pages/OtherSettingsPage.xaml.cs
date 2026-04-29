using System.Windows;
using System.Windows.Controls;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class OtherSettingsPage : UserControl
{
    private SettingsViewModel? _vm;

    public OtherSettingsPage()
    {
        InitializeComponent();
    }

    public void Initialize(SettingsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
    }

    private async void OtherSettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.LoadCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_vm == null) return;

        // Messages
        if (!string.IsNullOrEmpty(_vm.ErrorMessage))
        {
            ErrorText.Text = _vm.ErrorMessage;
            ErrorText.Visibility = Visibility.Visible;
        }
        else ErrorText.Visibility = Visibility.Collapsed;

        if (!string.IsNullOrEmpty(_vm.SuccessMessage))
        {
            SuccessText.Text = _vm.SuccessMessage;
            SuccessText.Visibility = Visibility.Visible;
        }
        else SuccessText.Visibility = Visibility.Collapsed;
    }

    // === Version History ===

    private async void LoadVersionHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.LoadVersionHistoryCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void DownloadSpecificVersion_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.DownloadSpecificVersionCommand.ExecuteAsync(null);
        UpdateUI();
    }

    // === Auto Update ===

    private async void CheckForUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.CheckForUpdateCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.InstallUpdateCommand.ExecuteAsync(null);
        UpdateUI();
    }
}
