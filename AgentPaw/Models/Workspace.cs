using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("workspace")]
public class Workspace
{
    [Key]
    [Column("workspace_id")]
    public string WorkspaceId { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("last_active_engine")]
    public string? LastActiveEngine { get; set; }

    [Column("context_window_size")]
    public int ContextWindowSize { get; set; } = 32000;

    [Column("auto_snapshot_enabled")]
    public bool AutoSnapshotEnabled { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
