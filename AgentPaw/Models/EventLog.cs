using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("event_log")]
public class EventLog
{
    [Key]
    [Column("event_id")]
    public string EventId { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    [Column("payload")]
    public string Payload { get; set; } = string.Empty;

    [Column("model_used")]
    public string? ModelUsed { get; set; }

    [Column("triggered_by")]
    public string? TriggeredBy { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; } = false;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
