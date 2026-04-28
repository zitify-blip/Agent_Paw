using System.Windows;
using System.Windows.Controls;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class IntegrationSettingsPage : UserControl
{
    private SettingsViewModel? _vm;

    public IntegrationSettingsPage()
    {
        InitializeComponent();
    }

    public void Initialize(SettingsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
    }

    private async void IntegrationSettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.LoadCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_vm == null) return;

        // Slack Bot
        if (_vm.SlackBotRunning)
        {
            SlackBotStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
            SlackBotStatusText.Text = "실행 중";
        }
        else if (_vm.SlackBotEnabled)
        {
            SlackBotStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
            SlackBotStatusText.Text = "활성 (미실행)";
        }
        else
        {
            SlackBotStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136));
            SlackBotStatusText.Text = "비활성";
        }
        SlackBotToggleButton.Content = _vm.SlackBotEnabled ? "비활성화" : "활성화";

        // Slack tokens
        if (_vm.HasSlackBotToken)
        {
            SlackBotTokenInputPanel.Visibility = Visibility.Collapsed;
            SlackBotTokenSetPanel.Visibility = Visibility.Visible;
            SlackBotTokenStatus.Text = "xoxb-••••••••";
        }
        else
        {
            SlackBotTokenInputPanel.Visibility = Visibility.Visible;
            SlackBotTokenSetPanel.Visibility = Visibility.Collapsed;
        }

        if (_vm.HasSlackAppToken)
        {
            SlackAppTokenInputPanel.Visibility = Visibility.Collapsed;
            SlackAppTokenSetPanel.Visibility = Visibility.Visible;
            SlackAppTokenStatus.Text = "xapp-••••••••";
        }
        else
        {
            SlackAppTokenInputPanel.Visibility = Visibility.Visible;
            SlackAppTokenSetPanel.Visibility = Visibility.Collapsed;
        }

        // Slack Channels
        SlackChannelLinksPanel.ItemsSource = _vm.SlackChannelLinks;
        NoSlackChannelsText.Visibility = _vm.SlackChannelLinks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Telegram Bot
        if (_vm.TelegramBotRunning)
        {
            TelegramBotStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
            TelegramBotStatusText.Text = "실행 중";
        }
        else if (_vm.TelegramBotEnabled)
        {
            TelegramBotStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
            TelegramBotStatusText.Text = "활성 (미실행)";
        }
        else
        {
            TelegramBotStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136));
            TelegramBotStatusText.Text = "비활성";
        }
        TelegramBotToggleButton.Content = _vm.TelegramBotEnabled ? "비활성화" : "활성화";
        TelegramBotUsernameText.Text = string.IsNullOrEmpty(_vm.TelegramBotUsername)
            ? string.Empty
            : $"@{_vm.TelegramBotUsername}";

        if (_vm.HasTelegramBotToken)
        {
            TelegramBotTokenInputPanel.Visibility = Visibility.Collapsed;
            TelegramBotTokenSetPanel.Visibility = Visibility.Visible;
            TelegramBotTokenStatus.Text = "••••••••";
        }
        else
        {
            TelegramBotTokenInputPanel.Visibility = Visibility.Visible;
            TelegramBotTokenSetPanel.Visibility = Visibility.Collapsed;
        }

        // Telegram Chats
        TelegramChatLinksPanel.ItemsSource = _vm.TelegramChatLinks;
        NoTelegramChatsText.Visibility = _vm.TelegramChatLinks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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

    // === Slack ===

    private async void ToggleSlackBot_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.ToggleSlackBotEnabledCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void SaveSlackBotToken_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.SaveSlackBotTokenCommand.ExecuteAsync(SlackBotTokenInput.Password);
        SlackBotTokenInput.Password = string.Empty;
        UpdateUI();
    }

    private async void SaveSlackAppToken_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.SaveSlackAppTokenCommand.ExecuteAsync(SlackAppTokenInput.Password);
        SlackAppTokenInput.Password = string.Empty;
        UpdateUI();
    }

    private async void DeleteSlackBotToken_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.DeleteSlackTokenCommand.ExecuteAsync("SLACK_BOT_TOKEN");
        UpdateUI();
    }

    private async void DeleteSlackAppToken_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.DeleteSlackTokenCommand.ExecuteAsync("SLACK_APP_TOKEN");
        UpdateUI();
    }

    private async void RestartSlack_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.RestartSlackCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void RefreshSlackChannels_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.RefreshSlackChannelsCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void ToggleSlackChannel_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: SpaceLinkItem link }) return;
        await _vm.ToggleSlackChannelCommand.ExecuteAsync(link);
        UpdateUI();
    }

    private async void DeleteSlackChannel_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: SpaceLinkItem link }) return;
        await _vm.DeleteSlackChannelCommand.ExecuteAsync(link);
        UpdateUI();
    }

    // === Telegram Bot ===

    private async void ToggleTelegramBot_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.ToggleTelegramBotEnabledCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void SaveTelegramBotToken_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.SaveTelegramBotTokenCommand.ExecuteAsync(TelegramBotTokenInput.Password);
        TelegramBotTokenInput.Password = string.Empty;
        UpdateUI();
    }

    private async void DeleteTelegramBotToken_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.DeleteTelegramBotTokenCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void RestartTelegram_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.RestartTelegramCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void RefreshTelegramChats_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.RefreshTelegramChatsCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void ToggleTelegramChat_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: SpaceLinkItem link }) return;
        await _vm.ToggleTelegramChatCommand.ExecuteAsync(link);
        UpdateUI();
    }

    private async void DeleteTelegramChat_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: SpaceLinkItem link }) return;
        await _vm.DeleteTelegramChatCommand.ExecuteAsync(link);
        UpdateUI();
    }
}
