using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("chat_session_state")]
public class ChatSessionState
{
    [Key]
    [Column("session_id")]
    public string SessionId { get; set; } = string.Empty;

    [Column("link_id")]
    public string LinkId { get; set; } = string.Empty;

    [Column("active_project_id")]
    public string? ActiveProjectId { get; set; }

    [Column("active_persona_id")]
    public string? ActivePersonaId { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
