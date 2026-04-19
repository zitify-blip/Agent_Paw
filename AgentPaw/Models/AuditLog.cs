using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("audit_log")]
public class AuditLog
{
    [Key]
    [Column("audit_id")]
    public string AuditId { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("project_id")]
    public string? ProjectId { get; set; }

    [Column("action")]
    public string Action { get; set; } = string.Empty;

    [Column("target_user_id")]
    public string? TargetUserId { get; set; }

    [Column("detail")]
    public string? Detail { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
