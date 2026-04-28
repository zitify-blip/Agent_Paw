using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentPaw.Orchestrator;

/// <summary>
/// PM의 다자 토론(round-table) 블록을 파싱한다.
/// - ```discussion      : PM이 토론을 개시할 때 participants·rounds·topic을 지정한다.
/// - ```discussion_summary : PM이 토론 종료 후 합의·잔여 쟁점을 정리한다.
/// - ```stance         : 토론 참여자가 라운드 내 자기 입장(agree/object/extend)을 명시한다.
/// </summary>
public static class DiscussionBlockParser
{
    private static readonly Regex DiscussionPattern = new(
        @"```discussion\s*(\{.*?\})\s*```",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SummaryPattern = new(
        @"```discussion_summary\s*(\{.*?\})\s*```",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StancePattern = new(
        @"```stance\s*(\{.*?\})\s*```",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static DiscussionOpenResult ParseOpen(string content)
    {
        if (string.IsNullOrEmpty(content))
            return new DiscussionOpenResult { CleanedContent = content ?? string.Empty };

        var match = DiscussionPattern.Match(content);
        if (!match.Success)
            return new DiscussionOpenResult { CleanedContent = content };

        try
        {
            var payload = JsonSerializer.Deserialize<OpenPayload>(match.Groups[1].Value, JsonOpts);
            var participants = payload?.Participants?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList() ?? [];
            if (payload == null || participants.Count < 2 || string.IsNullOrWhiteSpace(payload.Topic))
                return new DiscussionOpenResult { CleanedContent = DiscussionPattern.Replace(content, "").Trim() };

            return new DiscussionOpenResult
            {
                HasOpen = true,
                Topic = payload.Topic!.Trim(),
                Participants = participants,
                Rounds = payload.Rounds is > 0 ? payload.Rounds.Value : 2,
                StanceHint = payload.StanceHint?.Trim() ?? string.Empty,
                CleanedContent = DiscussionPattern.Replace(content, "").Trim()
            };
        }
        catch
        {
            return new DiscussionOpenResult { CleanedContent = DiscussionPattern.Replace(content, "").Trim() };
        }
    }

    public static DiscussionSummaryResult ParseSummary(string content)
    {
        if (string.IsNullOrEmpty(content))
            return new DiscussionSummaryResult { CleanedContent = content ?? string.Empty };

        var match = SummaryPattern.Match(content);
        if (!match.Success)
            return new DiscussionSummaryResult { CleanedContent = content };

        try
        {
            var payload = JsonSerializer.Deserialize<SummaryPayload>(match.Groups[1].Value, JsonOpts);
            if (payload == null)
                return new DiscussionSummaryResult { CleanedContent = SummaryPattern.Replace(content, "").Trim() };

            return new DiscussionSummaryResult
            {
                HasSummary = true,
                Consensus = payload.Consensus?.Trim() ?? string.Empty,
                Disagreements = payload.Disagreements?.Trim() ?? string.Empty,
                NextStep = payload.NextStep?.Trim() ?? string.Empty,
                CleanedContent = SummaryPattern.Replace(content, "").Trim()
            };
        }
        catch
        {
            return new DiscussionSummaryResult { CleanedContent = SummaryPattern.Replace(content, "").Trim() };
        }
    }

    public static StanceResult ParseStance(string content)
    {
        if (string.IsNullOrEmpty(content))
            return new StanceResult { CleanedContent = content ?? string.Empty };

        var match = StancePattern.Match(content);
        if (!match.Success)
            return new StanceResult { CleanedContent = content };

        try
        {
            var payload = JsonSerializer.Deserialize<StancePayload>(match.Groups[1].Value, JsonOpts);
            var pos = NormalizePosition(payload?.Position);
            return new StanceResult
            {
                HasStance = true,
                Position = pos,
                Argument = payload?.Argument?.Trim() ?? string.Empty,
                NextSpeaker = payload?.NextSpeaker?.Trim() ?? string.Empty,
                CleanedContent = StancePattern.Replace(content, "").Trim()
            };
        }
        catch
        {
            return new StanceResult { CleanedContent = StancePattern.Replace(content, "").Trim() };
        }
    }

    private static string NormalizePosition(string? raw)
    {
        var v = raw?.Trim().ToLowerInvariant();
        return v switch
        {
            "agree" or "동의" => "agree",
            "object" or "oppose" or "반대" => "object",
            "extend" or "보완" or "supplement" => "extend",
            _ => "extend"
        };
    }

    private class OpenPayload
    {
        public string? Topic { get; set; }
        public List<string>? Participants { get; set; }
        public int? Rounds { get; set; }
        public string? StanceHint { get; set; }
    }

    private class SummaryPayload
    {
        public string? Consensus { get; set; }
        public string? Disagreements { get; set; }
        public string? NextStep { get; set; }
    }

    private class StancePayload
    {
        public string? Position { get; set; }
        public string? Argument { get; set; }
        public string? NextSpeaker { get; set; }
    }
}

public class DiscussionOpenResult
{
    public bool HasOpen { get; set; }
    public string Topic { get; set; } = string.Empty;
    public List<string> Participants { get; set; } = [];
    public int Rounds { get; set; }
    public string StanceHint { get; set; } = string.Empty;
    public string CleanedContent { get; set; } = string.Empty;
}

public class DiscussionSummaryResult
{
    public bool HasSummary { get; set; }
    public string Consensus { get; set; } = string.Empty;
    public string Disagreements { get; set; } = string.Empty;
    public string NextStep { get; set; } = string.Empty;
    public string CleanedContent { get; set; } = string.Empty;
}

public class StanceResult
{
    public bool HasStance { get; set; }
    public string Position { get; set; } = "extend";
    public string Argument { get; set; } = string.Empty;
    public string NextSpeaker { get; set; } = string.Empty;
    public string CleanedContent { get; set; } = string.Empty;
}
