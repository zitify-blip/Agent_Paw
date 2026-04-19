using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("persona")]
public class Persona
{
    [Key]
    [Column("persona_id")]
    public string PersonaId { get; set; } = string.Empty;

    [Column("project_id")]
    public string? ProjectId { get; set; }

    [Column("group_id")]
    public string? GroupId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("label")]
    public string Label { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("avatar")]
    public string Avatar { get; set; } = string.Empty;

    [Column("system_prompt")]
    public string SystemPrompt { get; set; } = string.Empty;

    [Column("instructions")]
    public string Instructions { get; set; } = string.Empty;

    [Column("keywords")]
    public string Keywords { get; set; } = string.Empty;

    [Column("icon")]
    public string Icon { get; set; } = "bot";

    [Column("color")]
    public string Color { get; set; } = "blue";

    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    [Column("is_builtin")]
    public bool IsBuiltin { get; set; } = false;

    [Column("is_pm")]
    public bool IsPm { get; set; } = false;

    [Column("primary_model")]
    public string PrimaryModel { get; set; } = "claude-sonnet";

    [Column("fallback_model")]
    public string? FallbackModel { get; set; }

    [Column("temperature")]
    public float Temperature { get; set; } = 0.7f;

    [Column("max_tokens")]
    public int MaxTokens { get; set; } = 4096;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
