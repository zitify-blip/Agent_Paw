using System.Text.Json;
using System.Text.RegularExpressions;

namespace AgentPaw.Orchestrator;

/// <summary>
/// 페르소나 응답 본문에서 ```wiki_save``` 펜스 블록을 추출한다.
/// 블록에 담긴 결정·명세·트러블슈팅 기록은 WikiService 를 통해 프로젝트 위키로 영속화된다.
/// 한 응답에 다수의 블록이 존재할 수 있으며, 모두 추출 후 원문에서 제거한다.
/// </summary>
public static class WikiSaveParser
{
    private static readonly Regex Pattern = new(
        @"```wiki_save\s*(\{.*?\})\s*```",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static WikiSaveResult Parse(string content)
    {
        var result = new WikiSaveResult { CleanedContent = content ?? string.Empty };
        if (string.IsNullOrEmpty(content)) return result;

        foreach (Match m in Pattern.Matches(content))
        {
            try
            {
                var payload = JsonSerializer.Deserialize<WikiSavePayload>(m.Groups[1].Value, JsonOpts);
                if (payload == null) continue;

                var title = payload.Title?.Trim();
                var body = payload.Content?.Trim();
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body)) continue;

                result.Saves.Add(new WikiSaveBlock
                {
                    Category = NormalizeCategory(payload.Category),
                    Title = title!,
                    Content = body!
                });
            }
            catch (JsonException) { /* malformed — skip */ }
        }

        if (result.Saves.Count > 0)
            result.CleanedContent = Pattern.Replace(result.CleanedContent, "").Trim();

        return result;
    }

    private static string NormalizeCategory(string? category)
    {
        var c = category?.Trim().ToUpperInvariant();
        return c switch
        {
            "ADR" or "WIKI_ADR" => "WIKI_ADR",
            "SPEC" or "WIKI_SPEC" => "WIKI_SPEC",
            "TROUBLE" or "WIKI_TROUBLE" => "WIKI_TROUBLE",
            _ => "WIKI_ADR"
        };
    }

    private class WikiSavePayload
    {
        public string? Category { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
    }
}

public class WikiSaveBlock
{
    public string Category { get; set; } = "WIKI_ADR";
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class WikiSaveResult
{
    public List<WikiSaveBlock> Saves { get; set; } = [];
    public string CleanedContent { get; set; } = string.Empty;
}
