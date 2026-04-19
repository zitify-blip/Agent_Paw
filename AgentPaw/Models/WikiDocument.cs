using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("wiki_document")]
public class WikiDocument
{
    [Key]
    [Column("wiki_id")]
    public string WikiId { get; set; } = string.Empty;

    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("category")]
    public string Category { get; set; } = string.Empty; // WIKI_ADR, WIKI_SPEC, WIKI_TROUBLE

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("source_event_id")]
    public string? SourceEventId { get; set; }

    [Column("version")]
    public int Version { get; set; } = 1;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
