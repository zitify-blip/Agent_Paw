using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentPaw.Orchestrator;

public static class HandoffParser
{
    private static readonly Regex FencedPattern = new(
        @"```handoff\s*(\{.*?\})\s*```",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static HandoffResult Parse(string content)
    {
        if (string.IsNullOrEmpty(content))
            return new HandoffResult { HasHandoff = false, CleanedContent = content ?? string.Empty };

        var match = FencedPattern.Match(content);
        if (!match.Success)
            return new HandoffResult { HasHandoff = false, CleanedContent = content.Trim() };

        try
        {
            var payload = JsonSerializer.Deserialize<HandoffPayload>(match.Groups[1].Value, JsonOpts);
            if (payload == null || string.IsNullOrWhiteSpace(payload.To))
                return new HandoffResult { HasHandoff = false, CleanedContent = FencedPattern.Replace(content, "").Trim() };

            var cleaned = FencedPattern.Replace(content, "").Trim();
            return new HandoffResult
            {
                HasHandoff = true,
                To = payload.To.Trim(),
                Request = payload.Request?.Trim() ?? string.Empty,
                CleanedContent = cleaned
            };
        }
        catch
        {
            return new HandoffResult { HasHandoff = false, CleanedContent = FencedPattern.Replace(content, "").Trim() };
        }
    }

    private class HandoffPayload
    {
        public string? To { get; set; }
        public string? Request { get; set; }
    }
}

public class HandoffResult
{
    public bool HasHandoff { get; set; }
    public string To { get; set; } = string.Empty;
    public string Request { get; set; } = string.Empty;
    public string CleanedContent { get; set; } = string.Empty;
}
