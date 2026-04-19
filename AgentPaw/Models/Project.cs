using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("project")]
public class Project
{
    [Key]
    [Column("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [Column("project_name")]
    public string ProjectName { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("hierarchy_type")]
    public string HierarchyType { get; set; } = "ROOT"; // ROOT, PARENT, CHILD

    [Column("parent_project_id")]
    public string? ParentProjectId { get; set; }

    [Column("owner_user_id")]
    public string OwnerUserId { get; set; } = string.Empty;

    [Column("git_repo_path")]
    public string GitRepoPath { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, ARCHIVED, DELETED

    [Column("ask_user_enabled")]
    public bool AskUserEnabled { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
