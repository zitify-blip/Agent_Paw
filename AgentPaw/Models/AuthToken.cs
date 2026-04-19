using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("auth_token")]
public class AuthToken
{
    [Key]
    [Column("token_id")]
    public string TokenId { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("token_type")]
    public string TokenType { get; set; } = string.Empty; // GOOGLE_REFRESH, GOOGLE_SPACE_REFRESH, APP_SESSION

    [Column("token_value")]
    public string TokenValue { get; set; } = string.Empty;

    [Column("device_name")]
    public string? DeviceName { get; set; }

    [Column("device_ip")]
    public string? DeviceIp { get; set; }

    [Column("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [Column("is_revoked")]
    public bool IsRevoked { get; set; } = false;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
