namespace AgentPaw.Services;

/// <summary>
/// 외부 챗봇 메시지에서 "위키에서 조회" 의도를 명시적으로 감지한다.
/// 자연어 전체 의도 분류는 오탐 위험이 크므로, 다음 조건을 **모두** 만족할 때만 위키 질의로 라우팅한다:
///   1) "위키" 키워드가 본문에 포함
///   2) 조회성 동사("보여/찾/알려/검색/조회/가져/불러/뭐")가 포함
///   3) 생성성 동사("작성/만들/고쳐/수정/추가/짜")가 포함되지 않음 (위키 "작성"은 시스템 자동 경로만 허용)
/// 그 외의 암묵적 자연어 질의는 오케스트레이터(+Wiki 컨텍스트 주입, 3단계)가 담당한다.
/// </summary>
public static class WikiIntentDetector
{
    private static readonly string[] RetrieveVerbs =
    [
        "보여", "찾", "알려", "검색", "조회", "가져", "불러", "뭐였", "뭐야", "있어", "있나"
    ];

    private static readonly string[] CreateVerbs =
    [
        "작성", "만들", "짜줘", "짜봐", "고쳐", "수정", "바꿔", "추가", "초안", "발행", "기록해"
    ];

    public static WikiIntent? TryDetect(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (!text.Contains("위키", StringComparison.OrdinalIgnoreCase))
            return null;

        var hasRetrieve = RetrieveVerbs.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
        if (!hasRetrieve) return null;

        var hasCreate = CreateVerbs.Any(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
        if (hasCreate) return null;

        // 핵심 키워드 추출 — "위키", 조회 동사, 조사/어미를 제거
        var cleaned = text;
        foreach (var v in RetrieveVerbs.Concat(["위키", "에서", "에", "를", "을", "좀", "해줘", "줘", "요", "?", "!"]))
        {
            cleaned = cleaned.Replace(v, " ", StringComparison.OrdinalIgnoreCase);
        }
        var keyword = string.Join(' ',
            cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return new WikiIntent { Keyword = keyword };
    }
}

public class WikiIntent
{
    public string Keyword { get; set; } = string.Empty;
}
