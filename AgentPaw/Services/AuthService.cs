using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

public class AuthService
{
    private const int MaxActiveSessions = 3;
    private static readonly TimeSpan JwtExpiry = TimeSpan.FromHours(24);

    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly EncryptionService _encryption;
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _redirectUri;
    private string? _jwtSecret;

    public string? CurrentUserId { get; private set; }
    public string? CurrentTokenId { get; private set; }

    public bool IsDevBypassEnabled { get; }

    public AuthService(
        IDbContextFactory<AgentPawDbContext> dbFactory,
        EncryptionService encryption,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _dbFactory = dbFactory;
        _encryption = encryption;
        _httpClient = httpClientFactory.CreateClient();
        _clientId = configuration["Google:ClientId"] ?? string.Empty;
        _clientSecret = configuration["Google:ClientSecret"] ?? string.Empty;
        _redirectUri = configuration["Google:RedirectUri"] ?? "http://localhost:47891/auth/callback";
        IsDevBypassEnabled = configuration.GetValue<bool>("DevBypass");
    }

    public string GetLoginUrl()
    {
        var scopes = Uri.EscapeDataString("openid email profile https://www.googleapis.com/auth/documents");
        return $"https://accounts.google.com/o/oauth2/v2/auth?client_id={_clientId}&redirect_uri={Uri.EscapeDataString(_redirectUri)}&response_type=code&scope={scopes}&access_type=offline&prompt=consent";
    }

    public int GetRedirectPort() => new Uri(_redirectUri).Port;

    public async Task<(string Token, User User)> ExchangeCodeAndLoginAsync(string code, string? deviceName = null)
    {
        // Exchange code for tokens
        var tokenResponse = await ExchangeCodeAsync(code);
        var userInfo = DecodeIdToken(tokenResponse.IdToken);

        await using var db = await _dbFactory.CreateDbContextAsync();

        // Upsert user
        var user = await db.Users.FirstOrDefaultAsync(u => u.OauthUid == userInfo.Sub);
        if (user == null)
        {
            user = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = userInfo.Email,
                DisplayName = userInfo.Name,
                ProfileImageUrl = userInfo.Picture,
                OauthProvider = "GOOGLE",
                OauthUid = userInfo.Sub,
                LastLoginAt = DateTimeOffset.UtcNow,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);
        }
        else
        {
            if (!user.IsActive)
                throw new InvalidOperationException("ACCOUNT_INACTIVE");

            user.DisplayName = userInfo.Name;
            user.ProfileImageUrl = userInfo.Picture;
            user.LastLoginAt = DateTimeOffset.UtcNow;
        }

        // Save refresh token
        if (!string.IsNullOrEmpty(tokenResponse.RefreshToken))
        {
            var existingRefresh = await db.AuthTokens
                .FirstOrDefaultAsync(t => t.UserId == user.UserId && t.TokenType == "GOOGLE_REFRESH" && !t.IsRevoked);

            if (existingRefresh != null)
            {
                existingRefresh.TokenValue = _encryption.Encrypt(tokenResponse.RefreshToken);
            }
            else
            {
                db.AuthTokens.Add(new AuthToken
                {
                    TokenId = Guid.NewGuid().ToString(),
                    UserId = user.UserId,
                    TokenType = "GOOGLE_REFRESH",
                    TokenValue = _encryption.Encrypt(tokenResponse.RefreshToken),
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }
        }

        // Enforce max sessions
        var activeSessions = await db.AuthTokens
            .Where(t => t.UserId == user.UserId && t.TokenType == "APP_SESSION" && !t.IsRevoked)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        while (activeSessions.Count >= MaxActiveSessions)
        {
            activeSessions[0].IsRevoked = true;
            activeSessions.RemoveAt(0);
        }

        // Issue JWT
        var jwt = IssueJwt(user.UserId, user.Email);
        var sessionToken = new AuthToken
        {
            TokenId = Guid.NewGuid().ToString(),
            UserId = user.UserId,
            TokenType = "APP_SESSION",
            TokenValue = _encryption.Encrypt(jwt),
            DeviceName = deviceName ?? Environment.MachineName,
            ExpiresAt = DateTimeOffset.UtcNow.Add(JwtExpiry),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AuthTokens.Add(sessionToken);

        // Audit log
        db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            UserId = user.UserId,
            Action = "LOGIN",
            Detail = JsonSerializer.Serialize(new { deviceName = sessionToken.DeviceName }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        CurrentUserId = user.UserId;
        CurrentTokenId = sessionToken.TokenId;

        // 세션을 로컬에 자동 저장
        PersistSession(jwt, sessionToken.TokenId);

        return (jwt, user);
    }

    public (string UserId, string Email)? VerifyToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            handler.MapInboundClaims = false; // sub, email 클레임명을 그대로 유지
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtSecret()));
            var result = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = "agent-paw",
                ValidateAudience = true,
                ValidAudience = "agent-paw-desktop",
                ValidateLifetime = true,
                IssuerSigningKey = key
            }, out _);

            var userId = result.FindFirst("sub")?.Value;
            var email = result.FindFirst("email")?.Value;
            if (userId == null || email == null) return null;
            return (userId, email);
        }
        catch
        {
            return null;
        }
    }

    public async Task<SessionInfo?> GetSessionAsync()
    {
        if (CurrentUserId == null || CurrentTokenId == null) return null;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var token = await db.AuthTokens.FindAsync(CurrentTokenId);
        if (token == null || token.IsRevoked || (token.ExpiresAt.HasValue && token.ExpiresAt < DateTimeOffset.UtcNow))
            return null;

        var user = await db.Users.FindAsync(CurrentUserId);
        if (user == null || !user.IsActive) return null;

        return new SessionInfo
        {
            UserId = user.UserId,
            Email = user.Email,
            DisplayName = user.DisplayName,
            ProfileImageUrl = user.ProfileImageUrl
        };
    }

    public async Task LogoutAsync()
    {
        if (CurrentTokenId == null) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var token = await db.AuthTokens.FindAsync(CurrentTokenId);
        if (token != null)
        {
            token.IsRevoked = true;

            db.AuditLogs.Add(new AuditLog
            {
                AuditId = Guid.NewGuid().ToString(),
                UserId = CurrentUserId ?? string.Empty,
                Action = "LOGOUT",
                CreatedAt = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync();
        }

        CurrentUserId = null;
        CurrentTokenId = null;
    }

    public async Task LogoutAllAsync()
    {
        if (CurrentUserId == null) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var sessions = await db.AuthTokens
            .Where(t => t.UserId == CurrentUserId && t.TokenType == "APP_SESSION" && !t.IsRevoked)
            .ToListAsync();

        foreach (var s in sessions)
            s.IsRevoked = true;

        await db.SaveChangesAsync();
        CurrentUserId = null;
        CurrentTokenId = null;
    }

    public async Task<List<SessionEntry>> GetSessionsAsync()
    {
        if (CurrentUserId == null) return [];

        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.AuthTokens
            .Where(t => t.UserId == CurrentUserId && t.TokenType == "APP_SESSION" && !t.IsRevoked)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new SessionEntry
            {
                TokenId = t.TokenId,
                DeviceName = t.DeviceName,
                CreatedAt = t.CreatedAt,
                ExpiresAt = t.ExpiresAt,
                IsCurrent = t.TokenId == CurrentTokenId
            })
            .ToListAsync();
    }

    public async Task RevokeSessionAsync(string tokenId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var token = await db.AuthTokens.FindAsync(tokenId);
        if (token != null && token.UserId == CurrentUserId)
        {
            token.IsRevoked = true;

            db.AuditLogs.Add(new AuditLog
            {
                AuditId = Guid.NewGuid().ToString(),
                UserId = CurrentUserId ?? string.Empty,
                Action = "SESSION_FORCE_REVOKE",
                Detail = JsonSerializer.Serialize(new { revokedTokenId = tokenId }),
                CreatedAt = DateTimeOffset.UtcNow
            });

            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 로그인 성공 후 JWT와 tokenId를 로컬 파일에 저장한다.
    /// </summary>
    public void PersistSession(string jwt, string tokenId)
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AgentPaw", "data");
        Directory.CreateDirectory(dataDir);
        var sessionPath = Path.Combine(dataDir, ".session");
        File.WriteAllText(sessionPath, $"{tokenId}\n{jwt}");
    }

    /// <summary>
    /// 앱 시작 시 저장된 세션을 복원한다. 유효하면 SessionInfo를 반환한다.
    /// </summary>
    public async Task<SessionInfo?> TryRestoreSessionAsync()
    {
        var sessionPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AgentPaw", "data", ".session");

        if (!File.Exists(sessionPath)) return null;

        try
        {
            var lines = File.ReadAllLines(sessionPath);
            if (lines.Length < 2) return null;

            var tokenId = lines[0].Trim();
            var jwt = lines[1].Trim();

            var verified = VerifyToken(jwt);
            if (verified == null) { ClearPersistedSession(); return null; }

            await using var db = await _dbFactory.CreateDbContextAsync();
            var token = await db.AuthTokens.FindAsync(tokenId);
            if (token == null || token.IsRevoked || (token.ExpiresAt.HasValue && token.ExpiresAt < DateTimeOffset.UtcNow))
            {
                ClearPersistedSession();
                return null;
            }

            var user = await db.Users.FindAsync(verified.Value.UserId);
            if (user == null || !user.IsActive) { ClearPersistedSession(); return null; }

            CurrentUserId = user.UserId;
            CurrentTokenId = tokenId;

            return new SessionInfo
            {
                UserId = user.UserId,
                Email = user.Email,
                DisplayName = user.DisplayName,
                ProfileImageUrl = user.ProfileImageUrl
            };
        }
        catch
        {
            ClearPersistedSession();
            return null;
        }
    }

    public void ClearPersistedSession()
    {
        var sessionPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AgentPaw", "data", ".session");
        if (File.Exists(sessionPath))
            File.Delete(sessionPath);
    }

    public async Task<(string Token, User User)> DevBypassLoginAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var user = await db.Users.FirstOrDefaultAsync(u => u.OauthUid == "dev-bypass");
        if (user == null)
        {
            user = new User
            {
                UserId = Guid.NewGuid().ToString(),
                Email = "dev@agentpaw.local",
                DisplayName = "Dev User",
                OauthProvider = "GOOGLE",
                OauthUid = "dev-bypass",
                LastLoginAt = DateTimeOffset.UtcNow,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(); // 유저 먼저 커밋 — FK 순서 보장
        }
        else
        {
            user.LastLoginAt = DateTimeOffset.UtcNow;
        }

        var activeSessions = await db.AuthTokens
            .Where(t => t.UserId == user.UserId && t.TokenType == "APP_SESSION" && !t.IsRevoked)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        while (activeSessions.Count >= MaxActiveSessions)
        {
            activeSessions[0].IsRevoked = true;
            activeSessions.RemoveAt(0);
        }

        var jwt = IssueJwt(user.UserId, user.Email);
        var sessionToken = new AuthToken
        {
            TokenId = Guid.NewGuid().ToString(),
            UserId = user.UserId,
            TokenType = "APP_SESSION",
            TokenValue = _encryption.Encrypt(jwt),
            DeviceName = $"DEV:{Environment.MachineName}",
            ExpiresAt = DateTimeOffset.UtcNow.Add(JwtExpiry),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.AuthTokens.Add(sessionToken);

        db.AuditLogs.Add(new AuditLog
        {
            AuditId = Guid.NewGuid().ToString(),
            UserId = user.UserId,
            Action = "DEV_BYPASS_LOGIN",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync();

        CurrentUserId = user.UserId;
        CurrentTokenId = sessionToken.TokenId;

        PersistSession(jwt, sessionToken.TokenId);

        return (jwt, user);
    }

    // --- Private helpers ---

    private string GetJwtSecret()
    {
        if (_jwtSecret != null) return _jwtSecret;

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AgentPaw", "data");
        Directory.CreateDirectory(dataDir);

        var secretPath = Path.Combine(dataDir, ".jwt_secret");
        if (File.Exists(secretPath))
        {
            _jwtSecret = File.ReadAllText(secretPath).Trim();
        }
        else
        {
            _jwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            File.WriteAllText(secretPath, _jwtSecret);
        }

        return _jwtSecret;
    }

    private string IssueJwt(string userId, string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtSecret()));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "agent-paw",
            audience: "agent-paw-desktop",
            claims: [
                new Claim("sub", userId),
                new Claim("email", email)
            ],
            expires: DateTime.UtcNow.Add(JwtExpiry),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<string?> GetGoogleAccessTokenAsync()
    {
        if (CurrentUserId == null) return null;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var stored = await db.AuthTokens.FirstOrDefaultAsync(
            t => t.UserId == CurrentUserId && t.TokenType == "GOOGLE_REFRESH" && !t.IsRevoked);
        if (stored == null) return null;

        var refreshToken = _encryption.Decrypt(stored.TokenValue);
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", body);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) return null;

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
    }

    private async Task<GoogleTokenResponse> ExchangeCodeAsync(string code)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = _redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await _httpClient.PostAsync("https://oauth2.googleapis.com/token", content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Google token exchange failed ({response.StatusCode}): {responseBody}");

        return JsonSerializer.Deserialize<GoogleTokenResponse>(responseBody)
               ?? throw new InvalidOperationException($"Failed to parse token response: {responseBody}");
    }

    private static GoogleUserInfo DecodeIdToken(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length != 3)
            throw new FormatException("Invalid ID token");

        var payload = parts[1];
        // Pad base64url
        payload = payload.Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        return JsonSerializer.Deserialize<GoogleUserInfo>(json)
               ?? throw new InvalidOperationException("Failed to decode ID token");
    }
}

// --- DTOs ---

public class SessionInfo
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
}

public class SessionEntry
{
    public string TokenId { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsCurrent { get; set; }
}

internal class GoogleTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;
}

internal class GoogleUserInfo
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("picture")]
    public string? Picture { get; set; }
}
