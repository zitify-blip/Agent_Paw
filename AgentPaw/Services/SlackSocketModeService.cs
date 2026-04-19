using SlackNet;
using SlackNet.Events;

namespace AgentPaw.Services;

public class SlackSocketModeService
{
    private readonly ChatBotConfigService _configService;
    private readonly ChatDispatcherService _dispatcher;
    private readonly SlackChatService _slackChatService;
    private readonly ChatCommandService _commandService;
    private ISlackSocketModeClient? _socketClient;
    private CancellationTokenSource? _cts;

    public bool IsRunning { get; private set; }

    public SlackSocketModeService(
        ChatBotConfigService configService,
        ChatDispatcherService dispatcher,
        SlackChatService slackChatService,
        ChatCommandService commandService)
    {
        _configService = configService;
        _dispatcher = dispatcher;
        _slackChatService = slackChatService;
        _commandService = commandService;
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        var enabled = await _configService.IsSlackEnabledAsync();
        if (!enabled) return;

        var configured = await _configService.IsSlackConfiguredAsync();
        if (!configured) return;

        var appToken = await _configService.GetConfigAsync("SLACK_APP_TOKEN");
        var botToken = await _configService.GetConfigAsync("SLACK_BOT_TOKEN");
        if (string.IsNullOrEmpty(appToken) || string.IsNullOrEmpty(botToken)) return;

        try
        {
            _cts = new CancellationTokenSource();

            var services = new SlackServiceBuilder()
                .UseApiToken(botToken)
                .UseAppLevelToken(appToken)
                .RegisterEventHandler<MessageEvent>(new SlackMessageHandler(
                    _dispatcher, _slackChatService, _commandService));

            _socketClient = services.GetSocketModeClient();
            await _socketClient.Connect();

            IsRunning = true;
        }
        catch
        {
            IsRunning = false;
        }
    }

    public Task StopAsync()
    {
        if (!IsRunning) return Task.CompletedTask;

        try
        {
            _cts?.Cancel();
            _socketClient?.Disconnect();
        }
        catch { }
        finally
        {
            _socketClient?.Dispose();
            _socketClient = null;
            _cts?.Dispose();
            _cts = null;
            IsRunning = false;
        }

        return Task.CompletedTask;
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }
}

internal class SlackMessageHandler : IEventHandler<MessageEvent>
{
    private readonly ChatDispatcherService _dispatcher;
    private readonly SlackChatService _slackChatService;
    private readonly ChatCommandService _commandService;

    public SlackMessageHandler(
        ChatDispatcherService dispatcher,
        SlackChatService slackChatService,
        ChatCommandService commandService)
    {
        _dispatcher = dispatcher;
        _slackChatService = slackChatService;
        _commandService = commandService;
    }

    public async Task Handle(MessageEvent message)
    {
        // 봇 자신의 메시지 무시
        if (!string.IsNullOrEmpty(message.BotId)) return;
        // subtype이 있으면 무시 (편집, 삭제 등)
        if (!string.IsNullOrEmpty(message.Subtype)) return;

        var channel = message.Channel;
        var text = _slackChatService.StripBotMention(message.Text ?? string.Empty);

        if (string.IsNullOrWhiteSpace(text)) return;

        // 채널 미등록 시 자동 등록
        var link = await _commandService.FindLinkBySpaceNameAsync(channel);
        if (link == null)
        {
            await _commandService.UpsertSpaceLinkAsync(channel, $"#{channel}", true, "slack");
        }

        await _dispatcher.HandleIncomingMessageAsync(channel, text, _slackChatService);
    }
}
