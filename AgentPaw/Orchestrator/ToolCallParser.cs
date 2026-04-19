using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentPaw.Orchestrator;

public static class ToolCallParser
{
    private static readonly Regex FencedPattern = new(
        @"```tool\s*(\{.*?\})\s*```",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static ToolCallParseResult Parse(string content)
    {
        var result = new ToolCallParseResult { CleanedContent = content ?? string.Empty };
        if (string.IsNullOrEmpty(content)) return result;

        var matches = FencedPattern.Matches(content);
        if (matches.Count == 0)
        {
            result.CleanedContent = content.Trim();
            return result;
        }

        foreach (Match m in matches)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<ToolCallPayload>(m.Groups[1].Value, JsonOpts);
                if (payload == null || string.IsNullOrWhiteSpace(payload.Name)) continue;

                var args = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                if (payload.Args != null)
                {
                    foreach (var kv in payload.Args)
                        args[kv.Key] = kv.Value;
                }

                result.Calls.Add(new ToolCall
                {
                    Name = payload.Name.Trim(),
                    Args = args,
                    RawJson = m.Groups[1].Value
                });
            }
            catch
            {
                // ignore malformed block
            }
        }

        result.CleanedContent = FencedPattern.Replace(content, "").Trim();
        return result;
    }

    private class ToolCallPayload
    {
        public string? Name { get; set; }
        public Dictionary<string, JsonElement>? Args { get; set; }
    }
}

public class ToolCallParseResult
{
    public List<ToolCall> Calls { get; set; } = [];
    public string CleanedContent { get; set; } = string.Empty;
    public bool HasCalls => Calls.Count > 0;
}

public class ToolCall
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object?> Args { get; set; } = [];
    public string RawJson { get; set; } = string.Empty;
}

public class ToolCallRecord
{
    public string Name { get; set; } = string.Empty;
    public string ArgsSummary { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Result { get; set; } = string.Empty;
}
