using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("instruction_file")]
public class InstructionFile
{
    [Key]
    [Column("file_id")]
    public string FileId { get; set; } = string.Empty;

    [Column("group_id")]
    public string? GroupId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
