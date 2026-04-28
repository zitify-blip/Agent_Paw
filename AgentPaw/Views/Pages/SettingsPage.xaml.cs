using System.Windows;
using System.Windows.Controls;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class SettingsPage : UserControl
{
    private SettingsViewModel? _vm;

    public SettingsPage()
    {
        InitializeComponent();
    }

    public void Initialize(SettingsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.LoadCommand.ExecuteAsync(null);
    }
}
