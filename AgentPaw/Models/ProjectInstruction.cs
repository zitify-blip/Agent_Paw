using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AgentPaw.Models;

[Table("project_instruction")]
[PrimaryKey(nameof(ProjectId), nameof(FileId))]
public class ProjectInstruction
{
    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("file_id")]
    public string FileId { get; set; } = string.Empty;
}
