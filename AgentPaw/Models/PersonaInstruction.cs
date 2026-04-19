using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AgentPaw.Models;

[Table("persona_instruction")]
[PrimaryKey(nameof(PersonaId), nameof(FileId))]
public class PersonaInstruction
{
    [Column("persona_id")]
    public string PersonaId { get; set; } = string.Empty;

    [Column("file_id")]
    public string FileId { get; set; } = string.Empty;
}
