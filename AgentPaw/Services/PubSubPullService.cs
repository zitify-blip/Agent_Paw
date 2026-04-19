using System.Text;
using System.Text.Json;
using Google.Cloud.PubSub.V1;

namespace AgentPaw.Services;

public class PubSubPullService
{
    private readonly ChatBotConfigService _configService;
    private readonly ChatDispatcherService _dispatcher;
    private SubscriberClient? _subscriber;
    private CancellationTokenSource? _cts;

    public bool IsRunning { get; private set; }

    public PubSubPullService(ChatBotConfigService configService, ChatDispatcherService dispatcher)
    {
        _configService = configService;
        _dispatcher = dispatcher;
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        var enabled = await _configService.IsBotEnabledAsync();
        if (!enabled) return;

        var configured = await _configService.IsBotConfiguredAsync();
        if (!configured) return;

        var subscriptionName = await _configService.GetConfigAsync("SUBSCRIPTION_NAME");
        var serviceAccountJson = await _configService.GetConfigAsync("SERVICE_ACCOUNT_JSON");
        if (string.IsNullOrEmpty(subscriptionName) || string.IsNullOrEmpty(serviceAccountJson)) return;

        try
        {
            // 서비스 계정 인증으로 SubscriberClient 생성
            var builder = new SubscriberClientBuilder
            {
                SubscriptionName = SubscriptionName.Parse(subscriptionName),
                JsonCredentials = serviceAccountJson
            };

            _subscriber = await builder.BuildAsync();
            _cts = new CancellationTokenSource();

            // 백그라운드에서 메시지 수신 시작
            _ = _subscriber.StartAsync(async (msg, ct) =>
            {
                try
                {
                    var json = System.Text.Encoding.UTF8.GetString(msg.Data.ToArray());
                    var chatEvent = JsonSerializer.Deserialize<ChatEvent>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (chatEvent != null)
                        await _dispatcher.HandleChatEventAsync(chatEvent);

                    return SubscriberClient.Reply.Ack;
                }
                catch
                {
                    return SubscriberClient.Reply.Ack; // 파싱 실패 시에도 ack (재시도 방지)
                }
            });

            IsRunning = true;
        }
        catch
        {
            IsRunning = false;
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning || _subscriber == null) return;

        try
        {
            await _subscriber.StopAsync(_cts?.Token ?? CancellationToken.None);
        }
        catch { }
        finally
        {
            _subscriber = null;
            _cts?.Dispose();
            _cts = null;
            IsRunning = false;
        }
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }
}
