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

        // WebSocket
        WsStatusText.Text = _vm.WebSocketRunning ? "포트 8765 — 실행 중" : "포트 8765 — 중지";
        WsToggleButton.Content = _vm.WebSocketRunning ? "중지" : "시작";

        // Spaces (Google)
        SpaceLinksPanel.ItemsSource = _vm.SpaceLinks;
        NoSpacesText.Visibility = _vm.SpaceLinks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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

}
