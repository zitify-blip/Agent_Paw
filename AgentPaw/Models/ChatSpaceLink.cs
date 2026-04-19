using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("chat_space_link")]
public class ChatSpaceLink
{
    [Key]
    [Column("link_id")]
    public string LinkId { get; set; } = string.Empty;

    [Column("space_name")]
    public string SpaceName { get; set; } = string.Empty;

    [Column("space_display_name")]
    public string SpaceDisplayName { get; set; } = string.Empty;

    [Column("platform")]
    public string Platform { get; set; } = "google";

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
