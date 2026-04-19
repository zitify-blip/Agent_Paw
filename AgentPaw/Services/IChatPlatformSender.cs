namespace AgentPaw.Services;

public interface IChatPlatformSender
{
    Task SendMessageAsync(string channelOrSpace, string text);
}
