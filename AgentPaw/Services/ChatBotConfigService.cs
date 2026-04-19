using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

public class ChatBotConfigService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly EncryptionService _encryption;

    private static readonly HashSet<string> SensitiveKeys = ["SERVICE_ACCOUNT_JSON", "SLACK_BOT_TOKEN", "SLACK_APP_TOKEN", "TELEGRAM_BOT_TOKEN"];

    public ChatBotConfigService(IDbContextFactory<AgentPawDbContext> dbFactory, EncryptionService encryption)
    {
        _dbFactory = dbFactory;
        _encryption = encryption;
    }

    public async Task<string?> GetConfigAsync(string key)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var config = await db.ChatBotConfigs.FindAsync(key);
        if (config == null) return null;

        return SensitiveKeys.Contains(key)
            ? _encryption.Decrypt(config.ConfigValue)
            : config.ConfigValue;
    }

    public async Task SetConfigAsync(string key, string value)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var storedValue = SensitiveKeys.Contains(key) ? _encryption.Encrypt(value) : value;

        var existing = await db.ChatBotConfigs.FindAsync(key);
        if (existing != null)
        {
            existing.ConfigValue = storedValue;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.ChatBotConfigs.Add(new ChatBotConfig
            {
                ConfigKey = key,
                ConfigValue = storedValue,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    public async Task DeleteConfigAsync(string key)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var config = await db.ChatBotConfigs.FindAsync(key);
        if (config != null)
        {
            db.ChatBotConfigs.Remove(config);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> IsBotEnabledAsync()
    {
        var val = await GetConfigAsync("BOT_ENABLED");
        return val == "true";
    }

    public async Task<bool> IsBotConfiguredAsync()
    {
        var sa = await GetConfigAsync("SERVICE_ACCOUNT_JSON");
        var sub = await GetConfigAsync("SUBSCRIPTION_NAME");
        return !string.IsNullOrEmpty(sa) && !string.IsNullOrEmpty(sub);
    }

    // Slack
    public async Task<bool> IsSlackEnabledAsync()
    {
        var val = await GetConfigAsync("SLACK_BOT_ENABLED");
        return val == "true";
    }

    public async Task<bool> IsSlackConfiguredAsync()
    {
        var botToken = await GetConfigAsync("SLACK_BOT_TOKEN");
        var appToken = await GetConfigAsync("SLACK_APP_TOKEN");
        return !string.IsNullOrEmpty(botToken) && !string.IsNullOrEmpty(appToken);
    }

    public async Task<SlackBotStatus> GetSlackStatusAsync(bool running)
    {
        return new SlackBotStatus
        {
            Enabled = await IsSlackEnabledAsync(),
            Configured = await IsSlackConfiguredAsync(),
            Running = running,
            HasBotToken = !string.IsNullOrEmpty(await GetConfigAsync("SLACK_BOT_TOKEN")),
            HasAppToken = !string.IsNullOrEmpty(await GetConfigAsync("SLACK_APP_TOKEN"))
        };
    }

    // Telegram
    public async Task<bool> IsTelegramEnabledAsync()
    {
        var val = await GetConfigAsync("TELEGRAM_BOT_ENABLED");
        return val == "true";
    }

    public async Task<bool> IsTelegramConfiguredAsync()
    {
        var token = await GetConfigAsync("TELEGRAM_BOT_TOKEN");
        return !string.IsNullOrEmpty(token);
    }

    public async Task<TelegramBotStatus> GetTelegramStatusAsync(bool running)
    {
        return new TelegramBotStatus
        {
            Enabled = await IsTelegramEnabledAsync(),
            Configured = await IsTelegramConfiguredAsync(),
            Running = running,
            HasBotToken = !string.IsNullOrEmpty(await GetConfigAsync("TELEGRAM_BOT_TOKEN"))
        };
    }

    // Google Chat
    public async Task<ChatBotStatus> GetStatusAsync(bool running)
    {
        return new ChatBotStatus
        {
            Enabled = await IsBotEnabledAsync(),
            Configured = await IsBotConfiguredAsync(),
            Running = running,
            GcpProjectId = await GetConfigAsync("GCP_PROJECT_ID"),
            TopicName = await GetConfigAsync("TOPIC_NAME"),
            SubscriptionName = await GetConfigAsync("SUBSCRIPTION_NAME"),
            HasServiceAccount = !string.IsNullOrEmpty(await GetConfigAsync("SERVICE_ACCOUNT_JSON"))
        };
    }
}

public class SlackBotStatus
{
    public bool Enabled { get; set; }
    public bool Configured { get; set; }
    public bool Running { get; set; }
    public bool HasBotToken { get; set; }
    public bool HasAppToken { get; set; }
}

public class TelegramBotStatus
{
    public bool Enabled { get; set; }
    public bool Configured { get; set; }
    public bool Running { get; set; }
    public bool HasBotToken { get; set; }
}

public class ChatBotStatus
{
    public bool Enabled { get; set; }
    public bool Configured { get; set; }
    public bool Running { get; set; }
    public string? GcpProjectId { get; set; }
    public string? TopicName { get; set; }
    public string? SubscriptionName { get; set; }
    public bool HasServiceAccount { get; set; }
}
