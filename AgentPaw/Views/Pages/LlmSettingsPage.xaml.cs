using System.Windows;
using System.Windows.Controls;
using AgentPaw.ViewModels;

namespace AgentPaw.Views.Pages;

public partial class LlmSettingsPage : UserControl
{
    private SettingsViewModel? _vm;

    public LlmSettingsPage()
    {
        InitializeComponent();
    }

    public void Initialize(SettingsViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
    }

    private async void LlmSettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        await _vm.LoadCommand.ExecuteAsync(null);
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_vm == null) return;

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
}
