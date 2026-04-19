using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgentPaw.Services;

namespace AgentPaw.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ApiKeyService _apiKeyService;
    private readonly ClaudeCliService _claudeCliService;
    private readonly ChatBotConfigService _chatBotConfigService;
    private readonly PubSubPullService _pubSubPullService;
    private readonly WebSocketServerService _webSocketServerService;
    private readonly GoogleChatService _googleChatService;
    private readonly ChatCommandService _chatCommandService;
    private readonly SlackChatService _slackChatService;
    private readonly SlackSocketModeService _slackSocketModeService;
    private readonly TelegramChatService _telegramChatService;
    private readonly TelegramPollingService _telegramPollingService;
    private readonly UpdateService _updateService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _successMessage;

    // API Keys
    [ObservableProperty] private bool _hasClaudeKey;
    [ObservableProperty] private bool _hasGeminiKey;
    [ObservableProperty] private string _claudeKeyInput = string.Empty;
    [ObservableProperty] private string _geminiKeyInput = string.Empty;
    [ObservableProperty] private string _claudeKeyHint = string.Empty;
    [ObservableProperty] private string _geminiKeyHint = string.Empty;

    // Claude CLI
    [ObservableProperty] private bool _claudeCliAvailable;
    [ObservableProperty] private bool _claudeCliEnabled;

    // Chat Bot
    [ObservableProperty] private bool _botEnabled;
    [ObservableProperty] private bool _botConfigured;
    [ObservableProperty] private bool _botRunning;
    [ObservableProperty] private string _gcpProjectId = string.Empty;
    [ObservableProperty] private string _topicName = string.Empty;
    [ObservableProperty] private string _subscriptionName = string.Empty;
    [ObservableProperty] private bool _hasServiceAccount;

    // Slack Bot
    [ObservableProperty] private bool _slackBotEnabled;
    [ObservableProperty] private bool _slackBotConfigured;
    [ObservableProperty] private bool _slackBotRunning;
    [ObservableProperty] private bool _hasSlackBotToken;
    [ObservableProperty] private bool _hasSlackAppToken;

    // Telegram Bot
    [ObservableProperty] private bool _telegramBotEnabled;
    [ObservableProperty] private bool _telegramBotConfigured;
    [ObservableProperty] private bool _telegramBotRunning;
    [ObservableProperty] private bool _hasTelegramBotToken;
    [ObservableProperty] private string? _telegramBotUsername;

    // WebSocket
    [ObservableProperty] private bool _webSocketRunning;

    // Auto Update (tracker §4.8 패턴)
    [ObservableProperty] private string _currentVersion = UpdateService.CurrentVersion;
    [ObservableProperty] private bool _isCheckingUpdate;
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string? _latestVersion;
    [ObservableProperty] private string? _updateReleaseNotes;
    [ObservableProperty] private int _updateDownloadProgress;
    [ObservableProperty] private bool _isDownloadingUpdate;
    private UpdateInfo? _pendingUpdate;

    // Space Links (Google)
    public ObservableCollection<SpaceLinkItem> SpaceLinks { get; } = [];

    // Slack Channels
    public ObservableCollection<SpaceLinkItem> SlackChannelLinks { get; } = [];

    // Telegram Chats
    public ObservableCollection<SpaceLinkItem> TelegramChatLinks { get; } = [];

    public SettingsViewModel(
        ApiKeyService apiKeyService,
        ClaudeCliService claudeCliService,
        ChatBotConfigService chatBotConfigService,
        PubSubPullService pubSubPullService,
        WebSocketServerService webSocketServerService,
        GoogleChatService googleChatService,
        ChatCommandService chatCommandService,
        SlackChatService slackChatService,
        SlackSocketModeService slackSocketModeService,
        TelegramChatService telegramChatService,
        TelegramPollingService telegramPollingService,
        UpdateService updateService)
    {
        _apiKeyService = apiKeyService;
        _claudeCliService = claudeCliService;
        _chatBotConfigService = chatBotConfigService;
        _pubSubPullService = pubSubPullService;
        _webSocketServerService = webSocketServerService;
        _googleChatService = googleChatService;
        _chatCommandService = chatCommandService;
        _slackChatService = slackChatService;
        _slackSocketModeService = slackSocketModeService;
        _telegramChatService = telegramChatService;
        _telegramPollingService = telegramPollingService;
        _updateService = updateService;
    }

    [RelayCommand]
    public async Task CheckForUpdateAsync()
    {
        IsCheckingUpdate = true;
        ErrorMessage = null;
        SuccessMessage = null;
        try
        {
            var info = await _updateService.CheckForUpdateAsync();
            if (info == null)
            {
                UpdateAvailable = false;
                LatestVersion = null;
                UpdateReleaseNotes = null;
                _pendingUpdate = null;
                SuccessMessage = $"최신 버전(v{CurrentVersion})을 사용 중이다.";
            }
            else
            {
                UpdateAvailable = true;
                LatestVersion = info.Version;
                UpdateReleaseNotes = info.ReleaseNotes;
                _pendingUpdate = info;
                SuccessMessage = $"새 버전 v{info.Version} 이 사용 가능하다.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"업데이트 확인 실패: {ex.Message}";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    public async Task InstallUpdateAsync()
    {
        if (_pendingUpdate == null) return;

        IsDownloadingUpdate = true;
        UpdateDownloadProgress = 0;
        ErrorMessage = null;
        try
        {
            var ok = await _updateService.DownloadAndInstallAsync(
                _pendingUpdate,
                pct => UpdateDownloadProgress = pct);

            if (!ok)
            {
                ErrorMessage = "업데이트 다운로드·검증에 실패했다. SHA256 무결성 검증 결과를 확인하라.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"업데이트 설치 실패: {ex.Message}";
        }
        finally
        {
            IsDownloadingUpdate = false;
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            // API Keys
            HasClaudeKey = await _apiKeyService.HasApiKeyAsync("CLAUDE");
            HasGeminiKey = await _apiKeyService.HasApiKeyAsync("GEMINI");
            ClaudeKeyHint = HasClaudeKey ? await _apiKeyService.GetKeyHintAsync("CLAUDE") : string.Empty;
            GeminiKeyHint = HasGeminiKey ? await _apiKeyService.GetKeyHintAsync("GEMINI") : string.Empty;

            // Claude CLI
            ClaudeCliAvailable = await _claudeCliService.IsAvailableAsync();
            ClaudeCliEnabled = await _claudeCliService.IsEnabledAsync();

            // Chat Bot — 테이블 미존재 시에도 나머지 설정은 정상 로드
            try
            {
                var status = await _chatBotConfigService.GetStatusAsync(_pubSubPullService.IsRunning);
                BotEnabled = status.Enabled;
                BotConfigured = status.Configured;
                BotRunning = status.Running;
                GcpProjectId = status.GcpProjectId ?? string.Empty;
                TopicName = status.TopicName ?? string.Empty;
                SubscriptionName = status.SubscriptionName ?? string.Empty;
                HasServiceAccount = status.HasServiceAccount;
            }
            catch (Exception)
            {
                // chat_bot_config 테이블이 아직 없을 수 있음 — 기본값 유지
                BotEnabled = false;
                BotConfigured = false;
                BotRunning = false;
            }

            // Slack Bot
            try
            {
                var slackStatus = await _chatBotConfigService.GetSlackStatusAsync(_slackSocketModeService.IsRunning);
                SlackBotEnabled = slackStatus.Enabled;
                SlackBotConfigured = slackStatus.Configured;
                SlackBotRunning = slackStatus.Running;
                HasSlackBotToken = slackStatus.HasBotToken;
                HasSlackAppToken = slackStatus.HasAppToken;
            }
            catch
            {
                SlackBotEnabled = false;
                SlackBotConfigured = false;
                SlackBotRunning = false;
            }

            // Telegram Bot
            try
            {
                var tgStatus = await _chatBotConfigService.GetTelegramStatusAsync(_telegramPollingService.IsRunning);
                TelegramBotEnabled = tgStatus.Enabled;
                TelegramBotConfigured = tgStatus.Configured;
                TelegramBotRunning = tgStatus.Running;
                HasTelegramBotToken = tgStatus.HasBotToken;
                TelegramBotUsername = _telegramChatService.BotUsername;
            }
            catch
            {
                TelegramBotEnabled = false;
                TelegramBotConfigured = false;
                TelegramBotRunning = false;
            }

            // WebSocket
            WebSocketRunning = _webSocketServerService.IsRunning;

            // Space Links — 테이블 미존재 시 무시
            try
            {
                await LoadSpaceLinksAsync();
                await LoadSlackChannelLinksAsync();
                await LoadTelegramChatLinksAsync();
            }
            catch (Exception)
            {
                // space_link 테이블이 아직 없을 수 있음
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // === API Key 저장 ===

    [RelayCommand]
    private async Task SaveClaudeKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(ClaudeKeyInput)) return;
        try
        {
            await _apiKeyService.SetApiKeyAsync("CLAUDE", ClaudeKeyInput.Trim());
            HasClaudeKey = true;
            ClaudeKeyInput = string.Empty;
            SuccessMessage = "Claude API 키가 저장되었습니다.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task SaveGeminiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(GeminiKeyInput)) return;
        try
        {
            await _apiKeyService.SetApiKeyAsync("GEMINI", GeminiKeyInput.Trim());
            HasGeminiKey = true;
            GeminiKeyInput = string.Empty;
            SuccessMessage = "Gemini API 키가 저장되었습니다.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteApiKeyAsync(string provider)
    {
        try
        {
            await _apiKeyService.DeleteApiKeyAsync(provider);
            if (provider == "CLAUDE") { HasClaudeKey = false; ClaudeKeyHint = string.Empty; }
            else if (provider == "GEMINI") { HasGeminiKey = false; GeminiKeyHint = string.Empty; }
            SuccessMessage = $"{provider} API 키가 삭제되었습니다.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    // === Claude CLI ===

    [RelayCommand]
    private async Task ToggleClaudeCliAsync()
    {
        try
        {
            var newState = !ClaudeCliEnabled;
            await _claudeCliService.SetEnabledAsync(newState);
            ClaudeCliEnabled = newState;
            SuccessMessage = newState ? "Claude CLI 활성화됨" : "Claude CLI 비활성화됨";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    // === Chat Bot Config ===

    [RelayCommand]
    private async Task SaveBotConfigAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(GcpProjectId))
                await _chatBotConfigService.SetConfigAsync("GCP_PROJECT_ID", GcpProjectId.Trim());
            if (!string.IsNullOrWhiteSpace(TopicName))
                await _chatBotConfigService.SetConfigAsync("TOPIC_NAME", TopicName.Trim());
            if (!string.IsNullOrWhiteSpace(SubscriptionName))
                await _chatBotConfigService.SetConfigAsync("SUBSCRIPTION_NAME", SubscriptionName.Trim());

            SuccessMessage = "Chat Bot 설정이 저장되었습니다.";
            BotConfigured = await _chatBotConfigService.IsBotConfiguredAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ToggleBotEnabledAsync()
    {
        try
        {
            var newState = !BotEnabled;
            await _chatBotConfigService.SetConfigAsync("BOT_ENABLED", newState ? "true" : "false");
            BotEnabled = newState;

            if (newState && BotConfigured)
            {
                await _pubSubPullService.StartAsync();
                BotRunning = _pubSubPullService.IsRunning;
            }
            else
            {
                await _pubSubPullService.StopAsync();
                BotRunning = false;
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task RestartPubSubAsync()
    {
        try
        {
            await _pubSubPullService.RestartAsync();
            BotRunning = _pubSubPullService.IsRunning;
            SuccessMessage = BotRunning ? "PubSub 재시작 완료" : "PubSub 시작 실패";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    // === WebSocket ===

    [RelayCommand]
    private void ToggleWebSocket()
    {
        try
        {
            if (WebSocketRunning)
                _webSocketServerService.Stop();
            else
                _webSocketServerService.Start();

            WebSocketRunning = _webSocketServerService.IsRunning;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    // === Service Account Upload ===

    [RelayCommand]
    private async Task UploadServiceAccountAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            await _chatBotConfigService.SetConfigAsync("SERVICE_ACCOUNT_JSON", json);
            HasServiceAccount = true;
            _googleChatService.ResetAuthClient();
            SuccessMessage = "서비스 계정이 업로드되었습니다.";
            BotConfigured = await _chatBotConfigService.IsBotConfiguredAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    // === Space Links ===

    private async Task LoadSpaceLinksAsync()
    {
        var links = await _chatCommandService.ListLinksAsync("google");
        SpaceLinks.Clear();
        foreach (var l in links)
        {
            SpaceLinks.Add(new SpaceLinkItem
            {
                LinkId = l.LinkId,
                SpaceName = l.SpaceName,
                DisplayName = l.SpaceDisplayName,
                Enabled = l.Enabled
            });
        }
    }

    private async Task LoadSlackChannelLinksAsync()
    {
        var links = await _chatCommandService.ListLinksAsync("slack");
        SlackChannelLinks.Clear();
        foreach (var l in links)
        {
            SlackChannelLinks.Add(new SpaceLinkItem
            {
                LinkId = l.LinkId,
                SpaceName = l.SpaceName,
                DisplayName = l.SpaceDisplayName,
                Enabled = l.Enabled
            });
        }
    }

    [RelayCommand]
    private async Task RefreshSpacesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var spaces = await _googleChatService.ListSpacesAsync();
            foreach (var space in spaces)
            {
                await _chatCommandService.UpsertSpaceLinkAsync(space.Name, space.DisplayName, false);
            }
            await LoadSpaceLinksAsync();
            SuccessMessage = $"{spaces.Count}개 Space를 새로고침했습니다.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ToggleSpaceAsync(SpaceLinkItem link)
    {
        try
        {
            var newState = !link.Enabled;
            await _chatCommandService.SetLinkEnabledAsync(link.LinkId, newState);
            link.Enabled = newState;
            await LoadSpaceLinksAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteSpaceAsync(SpaceLinkItem link)
    {
        try
        {
            await _chatCommandService.DeleteLinkAsync(link.LinkId);
            await LoadSpaceLinksAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
    // === Slack Bot ===

    [RelayCommand]
    private async Task SaveSlackBotTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            await _chatBotConfigService.SetConfigAsync("SLACK_BOT_TOKEN", token.Trim());
            HasSlackBotToken = true;
            _slackChatService.ResetClient();
            SlackBotConfigured = await _chatBotConfigService.IsSlackConfiguredAsync();
            SuccessMessage = "Slack Bot Token이 저장되었습니다.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task SaveSlackAppTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            await _chatBotConfigService.SetConfigAsync("SLACK_APP_TOKEN", token.Trim());
            HasSlackAppToken = true;
            SlackBotConfigured = await _chatBotConfigService.IsSlackConfiguredAsync();
            SuccessMessage = "Slack App Token이 저장되었습니다.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteSlackTokenAsync(string key)
    {
        try
        {
            await _chatBotConfigService.DeleteConfigAsync(key);
            if (key == "SLACK_BOT_TOKEN") { HasSlackBotToken = false; _slackChatService.ResetClient(); }
            else if (key == "SLACK_APP_TOKEN") HasSlackAppToken = false;
            SlackBotConfigured = await _chatBotConfigService.IsSlackConfiguredAsync();
            SuccessMessage = "Slack 토큰이 삭제되었습니다.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task ToggleSlackBotEnabledAsync()
    {
        try
        {
            var newState = !SlackBotEnabled;
            await _chatBotConfigService.SetConfigAsync("SLACK_BOT_ENABLED", newState ? "true" : "false");
            SlackBotEnabled = newState;

            if (newState && SlackBotConfigured)
            {
                await _slackSocketModeService.StartAsync();
                SlackBotRunning = _slackSocketModeService.IsRunning;
            }
            else
            {
                await _slackSocketModeService.StopAsync();
                SlackBotRunning = false;
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task RestartSlackAsync()
    {
        try
        {
            await _slackSocketModeService.RestartAsync();
            SlackBotRunning = _slackSocketModeService.IsRunning;
            SuccessMessage = SlackBotRunning ? "Slack 재시작 완료" : "Slack 시작 실패";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task RefreshSlackChannelsAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var channels = await _slackChatService.ListChannelsAsync();
            foreach (var ch in channels)
            {
                await _chatCommandService.UpsertSpaceLinkAsync(ch.ChannelId, $"#{ch.ChannelName}", false, "slack");
            }
            await LoadSlackChannelLinksAsync();
            SuccessMessage = $"{channels.Count}개 Slack 채널을 새로고침했습니다.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ToggleSlackChannelAsync(SpaceLinkItem link)
    {
        try
        {
            var newState = !link.Enabled;
            await _chatCommandService.SetLinkEnabledAsync(link.LinkId, newState);
            link.Enabled = newState;
            await LoadSlackChannelLinksAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteSlackChannelAsync(SpaceLinkItem link)
    {
        try
        {
            await _chatCommandService.DeleteLinkAsync(link.LinkId);
            await LoadSlackChannelLinksAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    // === Telegram Bot ===

    private async Task LoadTelegramChatLinksAsync()
    {
        var links = await _chatCommandService.ListLinksAsync("telegram");
        TelegramChatLinks.Clear();
        foreach (var l in links)
        {
            TelegramChatLinks.Add(new SpaceLinkItem
            {
                LinkId = l.LinkId,
                SpaceName = l.SpaceName,
                DisplayName = l.SpaceDisplayName,
                Enabled = l.Enabled
            });
        }
    }

    [RelayCommand]
    private async Task SaveTelegramBotTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        try
        {
            await _chatBotConfigService.SetConfigAsync("TELEGRAM_BOT_TOKEN", token.Trim());
            _telegramChatService.ResetClient();
            HasTelegramBotToken = true;
            TelegramBotConfigured = await _chatBotConfigService.IsTelegramConfiguredAsync();

            // Bot 식별자(username) 검증·캐시
            try
            {
                var me = await _telegramChatService.GetMeAsync();
                TelegramBotUsername = me?.Username;
                SuccessMessage = me != null
                    ? $"Telegram Bot Token 저장 완료 — @{me.Username}"
                    : "Telegram Bot Token이 저장되었습니다.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"토큰은 저장되었지만 Telegram API 호출 실패: {ex.Message}";
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteTelegramBotTokenAsync()
    {
        try
        {
            await _chatBotConfigService.DeleteConfigAsync("TELEGRAM_BOT_TOKEN");
            _telegramChatService.ResetClient();
            HasTelegramBotToken = false;
            TelegramBotUsername = null;
            TelegramBotConfigured = await _chatBotConfigService.IsTelegramConfiguredAsync();
            SuccessMessage = "Telegram Bot Token이 삭제되었습니다.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task ToggleTelegramBotEnabledAsync()
    {
        try
        {
            var newState = !TelegramBotEnabled;
            await _chatBotConfigService.SetConfigAsync("TELEGRAM_BOT_ENABLED", newState ? "true" : "false");
            TelegramBotEnabled = newState;

            if (newState && TelegramBotConfigured)
            {
                await _telegramPollingService.StartAsync();
                TelegramBotRunning = _telegramPollingService.IsRunning;
            }
            else
            {
                await _telegramPollingService.StopAsync();
                TelegramBotRunning = false;
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task RestartTelegramAsync()
    {
        try
        {
            await _telegramPollingService.RestartAsync();
            TelegramBotRunning = _telegramPollingService.IsRunning;
            SuccessMessage = TelegramBotRunning ? "Telegram 재시작 완료" : "Telegram 시작 실패";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task RefreshTelegramChatsAsync()
    {
        try
        {
            await LoadTelegramChatLinksAsync();
            SuccessMessage = $"{TelegramChatLinks.Count}개 Telegram 채팅을 불러왔습니다.";
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task ToggleTelegramChatAsync(SpaceLinkItem link)
    {
        try
        {
            var newState = !link.Enabled;
            await _chatCommandService.SetLinkEnabledAsync(link.LinkId, newState);
            link.Enabled = newState;
            await LoadTelegramChatLinksAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteTelegramChatAsync(SpaceLinkItem link)
    {
        try
        {
            await _chatCommandService.DeleteLinkAsync(link.LinkId);
            await LoadTelegramChatLinksAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }
}

public class SpaceLinkItem
{
    public string LinkId { get; set; } = string.Empty;
    public string SpaceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
