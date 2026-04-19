using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace AgentPaw.Services;

public class TelegramPollingService
{
    private readonly ChatBotConfigService _configService;
    private readonly ChatDispatcherService _dispatcher;
    private readonly TelegramChatService _telegram;
    private readonly ChatCommandService _commandService;
    private readonly IHttpClientFactory _httpClientFactory;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private long _offset;

    public bool IsRunning { get; private set; }

    public TelegramPollingService(
        ChatBotConfigService configService,
        ChatDispatcherService dispatcher,
        TelegramChatService telegram,
        ChatCommandService commandService,
        IHttpClientFactory httpClientFactory)
    {
        _configService = configService;
        _dispatcher = dispatcher;
        _telegram = telegram;
        _commandService = commandService;
        _httpClientFactory = httpClientFactory;
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        var enabled = await _configService.IsTelegramEnabledAsync();
        if (!enabled) return;

        var configured = await _configService.IsTelegramConfiguredAsync();
        if (!configured) return;

        var token = await _configService.GetConfigAsync("TELEGRAM_BOT_TOKEN");
        if (string.IsNullOrEmpty(token)) return;

        try
        {
            // 봇 정체성 확인 (username 캐시 — 멘션 제거에 사용)
            _telegram.ResetClient();
            await _telegram.GetMeAsync();

            _cts = new CancellationTokenSource();
            _offset = 0;
            _loopTask = Task.Run(() => RunLoopAsync(token, _cts.Token));
            IsRunning = true;
        }
        catch
        {
            IsRunning = false;
        }
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        try
        {
            _cts?.Cancel();
            if (_loopTask != null) await _loopTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch { }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _loopTask = null;
            IsRunning = false;
        }
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    private async Task RunLoopAsync(string token, CancellationToken ct)
    {
        // HttpClient timeout은 long-poll 대기보다 길어야 한다
        var http = _httpClientFactory.CreateClient();
        http.Timeout = TimeSpan.FromSeconds(45);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{token}/getUpdates?timeout=30&offset={_offset}";
                var resp = await http.GetFromJsonAsync<TelegramResponse<List<TelegramUpdate>>>(url, ct);

                if (resp?.Ok == true && resp.Result != null)
                {
                    foreach (var upd in resp.Result)
                    {
                        if (upd.UpdateId >= _offset) _offset = upd.UpdateId + 1;
                        if (upd.Message == null) continue;
                        await HandleUpdateAsync(upd.Message);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch
            {
                // 네트워크 일시 오류 — 잠깐 쉬고 재시도
                try { await Task.Delay(3000, ct); } catch { break; }
            }
        }
    }

    private async Task HandleUpdateAsync(TelegramMessage message)
    {
        // 봇 자신 또는 빈 메시지 무시
        if (message.From?.IsBot == true) return;
        if (string.IsNullOrWhiteSpace(message.Text)) return;
        if (message.Chat == null) return;

        var chatId = message.Chat.Id.ToString();
        var chatName = message.Chat.Title
            ?? message.Chat.Username
            ?? message.Chat.FirstName
            ?? $"chat:{chatId}";

        // 그룹/슈퍼그룹은 @봇유저네임 멘션이 있을 때만 반응 (사용자 혼란 방지)
        var chatType = message.Chat.Type ?? "";
        var text = _telegram.StripBotMention(message.Text);
        var isGroup = chatType == "group" || chatType == "supergroup";

        if (isGroup)
        {
            var mentioned = !string.IsNullOrEmpty(_telegram.BotUsername)
                && message.Text.Contains($"@{_telegram.BotUsername}",
                    StringComparison.OrdinalIgnoreCase);
            if (!mentioned) return;
        }

        if (string.IsNullOrWhiteSpace(text)) return;

        // 채팅 미등록 시 자동 등록 (Slack과 동일한 패턴)
        var link = await _commandService.FindLinkBySpaceNameAsync(chatId);
        if (link == null)
        {
            await _commandService.UpsertSpaceLinkAsync(chatId, chatName, true, "telegram");
        }

        await _dispatcher.HandleIncomingMessageAsync(chatId, text, _telegram);
    }
}
