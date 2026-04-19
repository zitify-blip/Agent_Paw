using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
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
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_vm == null) return;

        // AI Engine API Keys
        if (_vm.HasClaudeKey)
        {
            ClaudeKeyInputPanel.Visibility = Visibility.Collapsed;
            ClaudeKeySetPanel.Visibility = Visibility.Visible;
            ClaudeKeyMasked.Text = _vm.ClaudeKeyHint;
        }
        else
        {
            ClaudeKeyInputPanel.Visibility = Visibility.Visible;
            ClaudeKeySetPanel.Visibility = Visibility.Collapsed;
        }

        if (_vm.HasGeminiKey)
        {
            GeminiKeyInputPanel.Visibility = Visibility.Collapsed;
            GeminiKeySetPanel.Visibility = Visibility.Visible;
            GeminiKeyMasked.Text = _vm.GeminiKeyHint;
        }
        else
        {
            GeminiKeyInputPanel.Visibility = Visibility.Visible;
            GeminiKeySetPanel.Visibility = Visibility.Collapsed;
        }

        // Bot status
        if (_vm.BotRunning)
        {
            BotStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
            BotStatusText.Text = "실행 중";
        }
        else if (_vm.BotEnabled)
        {
            BotStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
            BotStatusText.Text = "활성 (미실행)";
        }
        else
        {
            BotStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136));
            BotStatusText.Text = "비활성";
        }
        BotToggleButton.Content = _vm.BotEnabled ? "비활성화" : "활성화";

        GcpProjectInput.Text = _vm.GcpProjectId;
        TopicInput.Text = _vm.TopicName;
        SubscriptionInput.Text = _vm.SubscriptionName;
        ServiceAccountStatus.Text = _vm.HasServiceAccount ? "설정됨" : "미설정";

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

        // WebSocket
        WsStatusText.Text = _vm.WebSocketRunning ? "포트 8765 — 실행 중" : "포트 8765 — 중지";
        WsToggleButton.Content = _vm.WebSocketRunning ? "중지" : "시작";

        // Spaces (Google)
        SpaceLinksPanel.ItemsSource = _vm.SpaceLinks;
        NoSpacesText.Visibility = _vm.SpaceLinks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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

        // Claude CLI
        if (_vm.ClaudeCliAvailable && _vm.ClaudeCliEnabled)
        {
            ClaudeCliStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
            ClaudeCliStatusText.Text = "사용 가능 · 활성";
        }
        else if (_vm.ClaudeCliAvailable)
        {
            ClaudeCliStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
            ClaudeCliStatusText.Text = "사용 가능 · 비활성";
        }
        else
        {
            ClaudeCliStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136));
            ClaudeCliStatusText.Text = "CLI 미설치 (claude 명령어 없음)";
        }
        ClaudeCliToggleButton.Content = _vm.ClaudeCliEnabled ? "비활성화" : "활성화";
        ClaudeCliToggleButton.IsEnabled = _vm.ClaudeCliAvailable;

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

    // === Claude CLI ===

    private async void ToggleClaudeCli_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.ToggleClaudeCliCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void TestClaudeCli_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        // 다시 availability를 체크하고 UI 갱신
        await _vm.LoadCommand.ExecuteAsync(null);
        UpdateUI();
    }

    // === AI Engine API Keys ===

    private async void SaveClaudeKey_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || string.IsNullOrWhiteSpace(ClaudeKeyInput.Password)) return;
        _vm.ClaudeKeyInput = ClaudeKeyInput.Password;
        await _vm.SaveClaudeKeyCommand.ExecuteAsync(null);
        ClaudeKeyInput.Password = string.Empty;
        UpdateUI();
    }

    private async void DeleteClaudeKey_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.DeleteApiKeyCommand.ExecuteAsync("CLAUDE");
        UpdateUI();
    }

    private async void SaveGeminiKey_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || string.IsNullOrWhiteSpace(GeminiKeyInput.Password)) return;
        _vm.GeminiKeyInput = GeminiKeyInput.Password;
        await _vm.SaveGeminiKeyCommand.ExecuteAsync(null);
        GeminiKeyInput.Password = string.Empty;
        UpdateUI();
    }

    private async void DeleteGeminiKey_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.DeleteApiKeyCommand.ExecuteAsync("GEMINI");
        UpdateUI();
    }

    // === Chat Bot ===

    private async void ToggleBot_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.ToggleBotEnabledCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void SaveBotConfig_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.GcpProjectId = GcpProjectInput.Text;
        _vm.TopicName = TopicInput.Text;
        _vm.SubscriptionName = SubscriptionInput.Text;
        await _vm.SaveBotConfigCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void RestartPubSub_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.RestartPubSubCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private void ToggleWebSocket_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        _vm.ToggleWebSocketCommand.Execute(null);
        UpdateUI();
    }

    private async void UploadServiceAccount_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;

        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            Title = "Service Account JSON 파일 선택"
        };

        if (dialog.ShowDialog() == true)
        {
            var json = await System.IO.File.ReadAllTextAsync(dialog.FileName);
            await _vm.UploadServiceAccountCommand.ExecuteAsync(json);
            UpdateUI();
        }
    }

    private async void RefreshSpaces_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.RefreshSpacesCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private async void ToggleSpace_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: SpaceLinkItem link }) return;
        await _vm.ToggleSpaceCommand.ExecuteAsync(link);
        UpdateUI();
    }

    private async void DeleteSpace_Click(object sender, RoutedEventArgs e)
    {
        if (_vm == null || sender is not FrameworkElement { Tag: SpaceLinkItem link }) return;
        await _vm.DeleteSpaceCommand.ExecuteAsync(link);
        UpdateUI();
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
