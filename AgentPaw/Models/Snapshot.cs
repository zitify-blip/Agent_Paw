using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("snapshot")]
public class Snapshot
{
    [Key]
    [Column("snapshot_id")]
    public string SnapshotId { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("git_commit_hash")]
    public string GitCommitHash { get; set; } = string.Empty;

    [Column("vector_index_id")]
    public string VectorIndexId { get; set; } = string.Empty;

    [Column("trigger_type")]
    public string TriggerType { get; set; } = string.Empty; // MANUAL, AUTO, PRE_ROLLBACK

    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
