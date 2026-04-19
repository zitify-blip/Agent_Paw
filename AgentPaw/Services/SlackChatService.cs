using System.Text.RegularExpressions;
using SlackNet;
using SlackNet.WebApi;

namespace AgentPaw.Services;

public class SlackChatService : IChatPlatformSender
{
    private readonly ChatBotConfigService _configService;
    private ISlackApiClient? _client;
    private string? _botUserId;

    public SlackChatService(ChatBotConfigService configService)
    {
        _configService = configService;
    }

    private async Task<ISlackApiClient> GetClientAsync()
    {
        if (_client != null) return _client;

        var token = await _configService.GetConfigAsync("SLACK_BOT_TOKEN")
            ?? throw new InvalidOperationException("Slack Bot Token이 설정되지 않았습니다.");

        _client = new SlackServiceBuilder()
            .UseApiToken(token)
            .GetApiClient();

        // 봇 자신의 User ID 캐시 (멘션 제거용)
        try
        {
            var auth = await _client.Auth.Test();
            _botUserId = auth.UserId;
        }
        catch { }

        return _client;
    }

    public void ResetClient()
    {
        _client = null;
        _botUserId = null;
    }

    public async Task SendMessageAsync(string channelOrSpace, string text)
    {
        var client = await GetClientAsync();
        await client.Chat.PostMessage(new Message
        {
            Channel = channelOrSpace,
            Text = text
        });
    }

    public async Task<List<SlackChannelInfo>> ListChannelsAsync()
    {
        var client = await GetClientAsync();
        var channels = new List<SlackChannelInfo>();
        string? cursor = null;

        do
        {
            var response = await client.Conversations.List(
                cursor: cursor,
                types: [ConversationType.PublicChannel, ConversationType.PrivateChannel],
                limit: 200);

            foreach (var ch in response.Channels)
            {
                if (!ch.IsMember) continue;
                channels.Add(new SlackChannelInfo
                {
                    ChannelId = ch.Id,
                    ChannelName = ch.Name ?? ch.Id
                });
            }

            cursor = response.ResponseMetadata?.NextCursor;
        } while (!string.IsNullOrEmpty(cursor));

        return channels;
    }

    public string StripBotMention(string text)
    {
        if (_botUserId != null)
            text = Regex.Replace(text, $@"<@{Regex.Escape(_botUserId)}>", "");
        return text.Trim();
    }

    public string? BotUserId => _botUserId;
}

public class SlackChannelInfo
{
    public string ChannelId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
}
