using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;

namespace AgentPaw.Services;

public class GoogleChatService : IChatPlatformSender
{
    private readonly ChatBotConfigService _configService;
    private readonly IHttpClientFactory _httpFactory;
    private ServiceAccountCredential? _credential;

    public GoogleChatService(ChatBotConfigService configService, IHttpClientFactory httpFactory)
    {
        _configService = configService;
        _httpFactory = httpFactory;
    }

    public void ResetAuthClient()
    {
        _credential = null;
    }

    private async Task<ServiceAccountCredential> GetCredentialAsync()
    {
        if (_credential != null) return _credential;

        var json = await _configService.GetConfigAsync("SERVICE_ACCOUNT_JSON")
            ?? throw new InvalidOperationException("서비스 계정이 설정되지 않았습니다.");

        var initializer = new ServiceAccountCredential.Initializer("")
        {
            Scopes = ["https://www.googleapis.com/auth/chat.bot"]
        };

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var parsed = ServiceAccountCredential.FromServiceAccountData(stream);
        _credential = parsed;
        return _credential;
    }

    private async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var credential = await GetCredentialAsync();
        var token = await credential.GetAccessTokenForRequestAsync();

        var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<List<ChatSpace>> ListSpacesAsync()
    {
        var client = await GetAuthenticatedClientAsync();
        var spaces = new List<ChatSpace>();
        string? pageToken = null;

        do
        {
            var url = "https://chat.googleapis.com/v1/spaces";
            if (pageToken != null) url += $"?pageToken={pageToken}";

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("spaces", out var spacesArr))
            {
                foreach (var s in spacesArr.EnumerateArray())
                {
                    spaces.Add(new ChatSpace
                    {
                        Name = s.GetProperty("name").GetString() ?? string.Empty,
                        DisplayName = s.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? string.Empty : string.Empty
                    });
                }
            }

            pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var npt) ? npt.GetString() : null;
        } while (pageToken != null);

        return spaces;
    }

    public async Task CreateMessageAsync(string spaceName, string text)
    {
        var client = await GetAuthenticatedClientAsync();
        var url = $"https://chat.googleapis.com/v1/{spaceName}/messages";
        var body = JsonSerializer.Serialize(new { text });
        var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
    }

    public Task SendMessageAsync(string channelOrSpace, string text)
        => CreateMessageAsync(channelOrSpace, text);

    public static string StripBotMention(string text)
    {
        // @Bot 멘션 제거 (Google Chat 형식)
        return System.Text.RegularExpressions.Regex.Replace(text, @"<users/\d+>", "").Trim();
    }
}

public class ChatSpace
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
