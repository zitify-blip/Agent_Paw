using System.Text;
using AgentPaw.Models;
using AgentPaw.Services;

namespace AgentPaw.Orchestrator;

public class ContextInjectorService
{
    private readonly WikiService _wiki;

    // 한 턴 프롬프트에 동봉할 위키 최대 건수 — 컨텍스트 폭주를 방지한다.
    private const int MaxWikiDocs = 8;
    // 문서 본문 프리뷰 절단 길이 — 토큰 비용과 실질 근거 제공의 균형.
    private const int PreviewCharLimit = 300;

    public ContextInjectorService(WikiService wiki)
    {
        _wiki = wiki;
    }

    /// <summary>
    /// 첫 턴 프롬프트에 라우팅 메타데이터를 얹은 뒤, 관련 위키 문서를 요약 주입한다.
    /// 사용자 질의 토큰과 제목·본문 겹침이 큰 순 → 최신 순으로 정렬한 상위 8건만 포함한다.
    /// </summary>
    public async Task<string> InjectAsync(string userMessage, string projectId, string personaName, double confidence)
    {
        var routingNote = confidence < 0.7
            ? """

              [라우팅 주의]
              이 요청은 분류기가 확신하지 못해 임의로 당신에게 배정되었다.
              먼저 이 요청이 당신의 전문 영역인지 판단한다.
              당신의 영역이 아니라고 판단되면 혼자 답하지 말고, 반드시 handoff 블록으로 적절한 동료 페르소나에게 넘긴다.
              당신의 영역이 맞다면 평소처럼 응답한다.
              """
            : string.Empty;

        var baseText = $"""
            [Project ID: {projectId}]
            [Persona: {personaName}]
            [Confidence: {confidence:F2}]{routingNote}

            {userMessage}
            """;

        var wikiSection = await BuildWikiSectionAsync(projectId, userMessage);
        if (string.IsNullOrEmpty(wikiSection)) return baseText;

        return baseText + "\n\n" + wikiSection;
    }

    private async Task<string> BuildWikiSectionAsync(string projectId, string userMessage)
    {
        List<WikiDocument> wikis;
        try
        {
            wikis = await _wiki.ListWikisAsync(projectId);
        }
        catch
        {
            return string.Empty;
        }
        if (wikis.Count == 0) return string.Empty;

        var msgLower = userMessage.ToLowerInvariant();
        var tokens = ExtractTokens(msgLower);

        var ranked = wikis
            .Select(w => new { Doc = w, Score = OverlapScore(w, tokens) })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Doc.UpdatedAt)
            .Take(MaxWikiDocs)
            .Select(x => x.Doc)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("[프로젝트 위키 컨텍스트]");
        sb.AppendLine("이 프로젝트의 의사결정(ADR)·명세(SPEC)·트러블슈팅(TROUBLE) 기록이다.");
        sb.AppendLine("사용자 질문이 이 중 어느 항목과 연관되어 있다면 채팅 기록을 뒤지지 말고 아래 내용을 근거로 답한다.");
        sb.AppendLine();
        foreach (var w in ranked)
        {
            var cat = w.Category switch
            {
                "WIKI_ADR" => "ADR",
                "WIKI_SPEC" => "SPEC",
                "WIKI_TROUBLE" => "TROUBLE",
                _ => "DOC"
            };
            sb.AppendLine($"--- [{cat}] {w.Title} (업데이트: {w.UpdatedAt:yyyy-MM-dd}) ---");
            sb.AppendLine(Truncate(w.Content, PreviewCharLimit));
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static List<string> ExtractTokens(string msgLower)
    {
        var separators = new[] { ' ', '\t', '\n', '\r', '.', ',', '?', '!', ':', ';', '/', '\\', '(', ')', '[', ']', '"', '\'' };
        return msgLower
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2)
            .Distinct()
            .ToList();
    }

    private static int OverlapScore(WikiDocument doc, List<string> tokens)
    {
        if (tokens.Count == 0) return 0;
        var titleLower = doc.Title.ToLowerInvariant();
        var contentLower = doc.Content.ToLowerInvariant();
        var score = 0;
        foreach (var t in tokens)
        {
            if (titleLower.Contains(t)) score += 3;
            else if (contentLower.Contains(t)) score += 1;
        }
        return score;
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
        return text[..max] + "…";
    }
}
