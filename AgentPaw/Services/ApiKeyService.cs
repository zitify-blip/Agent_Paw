using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

public class ApiKeyService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly EncryptionService _encryption;

    public ApiKeyService(IDbContextFactory<AgentPawDbContext> dbFactory, EncryptionService encryption)
    {
        _dbFactory = dbFactory;
        _encryption = encryption;
    }

    public async Task SetApiKeyAsync(string provider, string plainKey)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.ApiKeyStores.FindAsync(provider);

        if (existing != null)
        {
            existing.EncryptedKey = _encryption.Encrypt(plainKey);
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            db.ApiKeyStores.Add(new ApiKeyStore
            {
                Provider = provider,
                EncryptedKey = _encryption.Encrypt(plainKey),
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task<string?> GetApiKeyAsync(string provider)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entry = await db.ApiKeyStores.FindAsync(provider);
        if (entry == null) return null;

        return _encryption.Decrypt(entry.EncryptedKey);
    }

    public async Task<bool> HasApiKeyAsync(string provider)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ApiKeyStores.AnyAsync(k => k.Provider == provider);
    }

    public async Task<string> GetKeyHintAsync(string provider)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entry = await db.ApiKeyStores.FindAsync(provider);
        if (entry == null) return string.Empty;

        var plain = _encryption.Decrypt(entry.EncryptedKey);
        if (string.IsNullOrEmpty(plain) || plain.Length < 8)
            return "••••••••";

        return plain[..4] + "••••••••" + plain[^4..];
    }

    public async Task DeleteApiKeyAsync(string provider)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var entry = await db.ApiKeyStores.FindAsync(provider);
        if (entry != null)
        {
            db.ApiKeyStores.Remove(entry);
            await db.SaveChangesAsync();
        }
    }

    public static string ModelToProvider(string model)
    {
        if (model.StartsWith("claude")) return "CLAUDE";
        if (model.StartsWith("gemini")) return "GEMINI";
        return "CLAUDE";
    }
}
