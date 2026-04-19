using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("project_member")]
public class ProjectMember
{
    [Key]
    [Column("member_id")]
    public string MemberId { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("role")]
    public string Role { get; set; } = string.Empty; // OWNER, EDITOR, VIEWER

    [Column("invited_by")]
    public string? InvitedBy { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
