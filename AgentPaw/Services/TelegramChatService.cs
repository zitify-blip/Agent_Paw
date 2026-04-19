using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AgentPaw.Services;

public class TelegramChatService : IChatPlatformSender
{
    private readonly ChatBotConfigService _configService;
    private readonly IHttpClientFactory _httpClientFactory;
    private string? _token;
    private string? _botUsername;
    private long? _botId;

    public TelegramChatService(ChatBotConfigService configService, IHttpClientFactory httpClientFactory)
    {
        _configService = configService;
        _httpClientFactory = httpClientFactory;
    }

    public void ResetClient()
    {
        _token = null;
        _botUsername = null;
        _botId = null;
    }

    public string? BotUsername => _botUsername;
    public long? BotId => _botId;

    private async Task<string> GetTokenAsync()
    {
        if (!string.IsNullOrEmpty(_token)) return _token;
        var token = await _configService.GetConfigAsync("TELEGRAM_BOT_TOKEN")
            ?? throw new InvalidOperationException("Telegram Bot Token이 설정되지 않았습니다.");
        _token = token;
        return token;
    }

    public async Task<TelegramUser?> GetMeAsync()
    {
        var token = await GetTokenAsync();
        var http = _httpClientFactory.CreateClient();
        var resp = await http.GetFromJsonAsync<TelegramResponse<TelegramUser>>(
            $"https://api.telegram.org/bot{token}/getMe");
        if (resp?.Ok == true && resp.Result != null)
        {
            _botId = resp.Result.Id;
            _botUsername = resp.Result.Username;
        }
        return resp?.Result;
    }

    public async Task SendMessageAsync(string channelOrSpace, string text)
    {
        var token = await GetTokenAsync();
        var http = _httpClientFactory.CreateClient();

        if (!long.TryParse(channelOrSpace, out var chatId))
            throw new InvalidOperationException($"Telegram chat id는 숫자여야 합니다: {channelOrSpace}");

        // Telegram은 단일 메시지 4096자 제한. 초과 시 분할 전송한다.
        const int max = 4000;
        var remaining = text ?? string.Empty;
        while (remaining.Length > 0)
        {
            var chunk = remaining.Length <= max ? remaining : remaining[..max];
            remaining = remaining.Length <= max ? string.Empty : remaining[max..];

            var payload = new { chat_id = chatId, text = chunk, disable_web_page_preview = true };
            var resp = await http.PostAsJsonAsync(
                $"https://api.telegram.org/bot{token}/sendMessage", payload);
            resp.EnsureSuccessStatusCode();
        }
    }

    public string StripBotMention(string text)
    {
        if (!string.IsNullOrEmpty(_botUsername))
            text = Regex.Replace(text, $@"@{Regex.Escape(_botUsername)}\b", "",
                RegexOptions.IgnoreCase);
        return text.Trim();
    }
}

public class TelegramResponse<T>
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("result")] public T? Result { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
}

public class TelegramUser
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("is_bot")] public bool IsBot { get; set; }
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
}

public class TelegramUpdate
{
    [JsonPropertyName("update_id")] public long UpdateId { get; set; }
    [JsonPropertyName("message")] public TelegramMessage? Message { get; set; }
}

public class TelegramMessage
{
    [JsonPropertyName("message_id")] public long MessageId { get; set; }
    [JsonPropertyName("from")] public TelegramUser? From { get; set; }
    [JsonPropertyName("chat")] public TelegramChat? Chat { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("date")] public long Date { get; set; }
}

public class TelegramChat
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
}

public class TelegramChatInfo
{
    public string ChatId { get; set; } = string.Empty;
    public string ChatName { get; set; } = string.Empty;
}
