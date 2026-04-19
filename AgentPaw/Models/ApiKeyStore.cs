using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgentPaw.Models;

[Table("api_key_store")]
public class ApiKeyStore
{
    [Key]
    [Column("provider")]
    public string Provider { get; set; } = string.Empty; // CLAUDE, GEMINI, CLAUDE_CLI_ENABLED

    [Column("encrypted_key")]
    public string EncryptedKey { get; set; } = string.Empty;

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
