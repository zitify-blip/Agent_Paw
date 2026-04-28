using System.Windows;
using System.Windows.Controls;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class LoginPage : UserControl
{
    public LoginPage()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is LoginViewModel vm && vm.IsDevBypassAvailable)
            {
                DevBypassButton.Visibility = Visibility.Visible;
                DevBypassButton.Click += async (_, _) => await vm.DevBypassLoginCommand.ExecuteAsync(null);
            }
        };
    }

    private async void GoogleLoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            await vm.LoginWithGoogleCommand.ExecuteAsync(null);
        }
    }
}
