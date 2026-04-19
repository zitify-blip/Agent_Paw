using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("chat_bot_config")]
public class ChatBotConfig
{
    [Key]
    [Column("config_key")]
    public string ConfigKey { get; set; } = string.Empty;

    [Column("config_value")]
    public string ConfigValue { get; set; } = string.Empty;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
