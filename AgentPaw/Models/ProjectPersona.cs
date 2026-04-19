using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AgentPaw.Models;

[Table("project_persona")]
[PrimaryKey(nameof(ProjectId), nameof(PersonaId))]
public class ProjectPersona
{
    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("persona_id")]
    public string PersonaId { get; set; } = string.Empty;
}
