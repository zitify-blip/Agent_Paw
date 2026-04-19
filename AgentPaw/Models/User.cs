using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("user")]
public class User
{
    [Key]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [Column("profile_image_url")]
    public string? ProfileImageUrl { get; set; }

    [Column("oauth_provider")]
    public string OauthProvider { get; set; } = "GOOGLE";

    [Column("oauth_uid")]
    public string OauthUid { get; set; } = string.Empty;

    [Column("last_login_at")]
    public DateTimeOffset? LastLoginAt { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
