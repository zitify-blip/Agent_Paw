using System.Windows;
using System.Windows.Controls;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class LoginPage : UserControl
{
    public LoginPage()
    {
        InitializeComponent();
#if DEBUG || DEVBYPASS
        DevBypassButton.Visibility = Visibility.Visible;
        DevBypassButton.Click += async (_, _) =>
        {
            if (DataContext is LoginViewModel vm)
                await vm.DevBypassLoginCommand.ExecuteAsync(null);
        };
#endif
    }

    private async void GoogleLoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
        {
            await vm.LoginWithGoogleCommand.ExecuteAsync(null);
        }
    }
}
