using System.Windows;
using System.Windows.Controls;
using AgentPaw.Models;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class ApiSettingsPage : UserControl
{
    public ApiSettingsPage()
    {
        InitializeComponent();
    }

    public void Initialize(ApiSettingsViewModel vm)
    {
        DataContext = vm;
    }

    private void EditPersona_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is Persona persona
            && DataContext is ApiSettingsViewModel vm)
        {
            vm.SelectPersonaCommand.Execute(persona);
        }
    }
}
