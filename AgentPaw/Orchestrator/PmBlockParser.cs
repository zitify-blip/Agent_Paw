using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentPaw.Orchestrator;

/// <summary>
/// PM 전용 응답 블록(pm_report, pm_intervention)을 파싱한다.
/// §7.4 PM 라우팅 프로토콜에 따라 PM만 이 블록들을 사용한다.
/// </summary>
public static class PmBlockParser
{
    private static readonly Regex ReportPattern = new(
        @"```pm_report\s*(\{.*?\})\s*```",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InterventionPattern = new(
        @"```pm_intervention\s*(\{.*?\})\s*```",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static PmBlockResult Parse(string content)
    {
        if (string.IsNullOrEmpty(content))
            return new PmBlockResult { CleanedContent = content ?? string.Empty };

        var result = new PmBlockResult { CleanedContent = content };

        var reportMatch = ReportPattern.Match(content);
        if (reportMatch.Success)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<ReportPayload>(reportMatch.Groups[1].Value, JsonOpts);
                if (payload != null)
                {
                    result.HasReport = true;
                    result.ReportSummary = payload.Summary?.Trim() ?? string.Empty;
                    result.ReportBody = payload.Body?.Trim() ?? string.Empty;
                    result.CleanedContent = ReportPattern.Replace(result.CleanedContent, "").Trim();
                }
            }
            catch
            {
                result.CleanedContent = ReportPattern.Replace(result.CleanedContent, "").Trim();
            }
        }

        var interventionMatch = InterventionPattern.Match(result.CleanedContent);
        if (interventionMatch.Success)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<InterventionPayload>(interventionMatch.Groups[1].Value, JsonOpts);
                if (payload != null)
                {
                    result.HasIntervention = true;
                    result.InterventionReason = payload.Reason?.Trim() ?? string.Empty;
                    result.InterventionQuestion = payload.Question?.Trim() ?? string.Empty;
                    result.CleanedContent = InterventionPattern.Replace(result.CleanedContent, "").Trim();
                }
            }
            catch
            {
                result.CleanedContent = InterventionPattern.Replace(result.CleanedContent, "").Trim();
            }
        }

        return result;
    }

    private class ReportPayload
    {
        public string? Summary { get; set; }
        public string? Body { get; set; }
    }

    private class InterventionPayload
    {
        public string? Reason { get; set; }
        public string? Question { get; set; }
    }
}

public class PmBlockResult
{
    public bool HasReport { get; set; }
    public string ReportSummary { get; set; } = string.Empty;
    public string ReportBody { get; set; } = string.Empty;

    public bool HasIntervention { get; set; }
    public string InterventionReason { get; set; } = string.Empty;
    public string InterventionQuestion { get; set; } = string.Empty;

    public string CleanedContent { get; set; } = string.Empty;
}
