using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("agent_team")]
public class AgentTeam
{
    [Key]
    [Column("team_id")]
    public string TeamId { get; set; } = Guid.NewGuid().ToString();

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    /// <summary>panel | debate | chain</summary>
    [Column("mode")]
    public string Mode { get; set; } = "panel";

    /// <summary>JSON array of PersonaId strings in execution order.</summary>
    [Column("member_persona_ids")]
    public string MemberPersonaIds { get; set; } = "[]";

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
