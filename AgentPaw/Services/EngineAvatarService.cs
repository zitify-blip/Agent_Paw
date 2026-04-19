using System.IO;

namespace AgentPaw.Services;

/// <summary>
/// 페르소나용 강아지 캐릭터 아바타 템플릿 + 엔진 아바타 제공.
/// Assets/Avatars/ 폴더의 PNG 파일을 data URI로 변환하여 제공한다.
///
/// 28종 강아지 협업자 컬렉션 — 각 아바타는 고유의 업무 소품과 함께 묘사되어
/// 페르소나의 역할/성격을 시각적으로 드러낸다.
/// </summary>
public static class EngineAvatarService
{
    private static readonly string AvatarsDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Assets", "Avatars");

    private static readonly Dictionary<string, string> _cache = new();

    public static List<AvatarTemplate> GetTemplates()
    {
        return
        [
            // Row 1 — 기획·문서 계열
            new("border-collie", "보더 콜리", "기획·문서", "#4A90D9", LoadAvatar("border_collie")),
            new("beagle", "비글", "탐색·리서치", "#C8924E", LoadAvatar("beagle")),
            new("miniature-pinscher", "미니핀", "알림·모니터링", "#8B6F47", LoadAvatar("miniature_pinscher")),
            new("cavalier", "카발리에", "검수·QA", "#C8A080", LoadAvatar("cavalier_king_charles")),
            new("japanese-chin", "재패니즈 친", "학습·교육", "#C07080", LoadAvatar("japanese_chin")),
            new("papillon", "빠삐용", "설계·디자인", "#A080C0", LoadAvatar("papillon")),
            new("toy-poodle", "토이 푸들", "창작·아트", "#C8A96E", LoadAvatar("toy_poodle")),

            // Row 2 — 구현·개발 계열
            new("pomeranian", "포메라니안", "디자인·스케치", "#D4A040", LoadAvatar("pomeranian")),
            new("pomeranian-coder", "포메라니안(코더)", "구현·코딩", "#D09030", LoadAvatar("pomeranian_2")),
            new("maltese", "말티즈", "출판·배포", "#7BA574", LoadAvatar("maltese")),
            new("bichon-frise", "비숑 프리제", "아이디어·기획", "#A0C080", LoadAvatar("bichon_frise")),
            new("shiba-inu", "시바 이누", "아키텍처·시스템", "#C87040", LoadAvatar("shiba_inu")),
            new("russell-terrier", "러셀 테리어", "조사·분석", "#6A9EC0", LoadAvatar("russell_terrier")),
            new("corgi", "코기", "빌드·조립", "#D8A860", LoadAvatar("corgi")),

            // Row 3 — 운영·인프라 계열
            new("australian-terrier", "오스트레일리언 테리어", "구조·설계", "#B08040", LoadAvatar("australian_terrier")),
            new("jack-russell", "잭 러셀", "에너지·추진", "#909090", LoadAvatar("jack_russell")),
            new("affenpinscher", "아펜핀셔", "마이닝·작업", "#505050", LoadAvatar("affenpinscher")),
            new("healthy-corgi", "헬시 코기", "건강·낙관", "#E0A070", LoadAvatar("healthy_corgi")),
            new("critical-schnauzer", "크리티컬 슈나우저", "긴급·대응", "#B04040", LoadAvatar("critical_schnauzer")),
            new("chihuahua", "치와와", "고부하·분산", "#6A9EC0", LoadAvatar("chihuahua")),
            new("japanese-chin-alt", "재패니즈 친(통합)", "오케스트레이션", "#B09080", LoadAvatar("japanese_chin_alt")),

            // Row 4 — 특수·보조 계열
            new("french-bulldog", "프렌치 불독", "놀이·친근", "#80A0C0", LoadAvatar("french_bulldog")),
            new("rollback-dachshund", "롤백 닥스훈트", "롤백·복구", "#C06040", LoadAvatar("rollback_dachshund")),
            new("miniature-schnauzer", "미니어처 슈나우저", "보안·방어", "#7BA574", LoadAvatar("miniature_schnauzer")),
            new("westie", "웨스티", "전원·시동", "#E8E0D0", LoadAvatar("westie")),
            new("border-terrier", "보더 테리어", "네트워크·연결", "#C8A078", LoadAvatar("border_terrier")),
            new("norfolk-terrier", "노퍽 테리어", "승인·검증", "#B08060", LoadAvatar("norfolk_terrier")),
            new("pug", "퍼그", "대기·휴식", "#D0B090", LoadAvatar("pug")),
        ];
    }

    public static string GetEngineAvatar(string provider)
    {
        return provider.ToUpperInvariant() switch
        {
            "CLAUDE" => LoadAvatar("border_collie"),
            "GEMINI" => LoadAvatar("miniature_schnauzer"),
            _ => LoadAvatar("cavalier_king_charles")
        };
    }

    /// <summary>품종 키(파일명)로 아바타 data URI를 반환한다. 기본 폴백은 corgi.</summary>
    public static string GetBreedAvatar(string breedKey)
    {
        if (string.IsNullOrWhiteSpace(breedKey)) return LoadAvatar("corgi");
        var loaded = LoadAvatar(breedKey);
        return string.IsNullOrEmpty(loaded) ? LoadAvatar("corgi") : loaded;
    }

    /// <summary>페르소나 이름·키워드·PM 여부로 가장 어울리는 강아지 아바타를 선택한다.</summary>
    public static string ResolveAvatarForPersona(string? name, string? keywords, bool isPm)
    {
        if (isPm) return LoadAvatar("border_collie");

        var kw = ((keywords ?? string.Empty) + " " + (name ?? string.Empty)).ToLowerInvariant();
        if (Contains(kw, "소설", "novel", "원고", "집필", "플롯", "시놉시스")) return LoadAvatar("toy_poodle");
        if (Contains(kw, "영상", "video", "시나리오", "스토리보드", "편집", "촬영")) return LoadAvatar("rollback_dachshund");
        if (Contains(kw, "디자인", "design", "designer", "ui", "ux", "브랜드")) return LoadAvatar("papillon");
        if (Contains(kw, "qa", "검수", "테스트", "검증")) return LoadAvatar("cavalier_king_charles");
        if (Contains(kw, "dba", "데이터베이스", "db ", "스키마")) return LoadAvatar("shiba_inu");
        if (Contains(kw, " da", "데이터 분석", "analytics", "분석")) return LoadAvatar("russell_terrier");
        if (Contains(kw, "aa ", "application architect", "애플리케이션")) return LoadAvatar("australian_terrier");
        if (Contains(kw, "sa ", "system architect", "아키텍처", "architect")) return LoadAvatar("shiba_inu");
        if (Contains(kw, "보안", "security", "방어")) return LoadAvatar("miniature_schnauzer");
        if (Contains(kw, "개발", "dev", "소프트웨어", "software", "코드", "code", "프로그래밍")) return LoadAvatar("border_collie");
        if (Contains(kw, "기획", "planner", "plan", "planning")) return LoadAvatar("bichon_frise");
        return LoadAvatar("corgi");
    }

    private static bool Contains(string haystack, params string[] needles)
    {
        foreach (var n in needles)
            if (!string.IsNullOrEmpty(n) && haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string LoadAvatar(string name)
    {
        if (_cache.TryGetValue(name, out var cached))
            return cached;

        var path = Path.Combine(AvatarsDir, $"{name}.png");
        if (!File.Exists(path))
            return string.Empty;

        var bytes = File.ReadAllBytes(path);
        var dataUri = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        _cache[name] = dataUri;
        return dataUri;
    }
}

/// <summary>아바타 템플릿 항목</summary>
public class AvatarTemplate
{
    public string Id { get; }
    public string BreedName { get; }
    public string RoleHint { get; }
    public string AccentColor { get; }
    public string DataUri { get; }

    public AvatarTemplate(string id, string breedName, string roleHint, string accentColor, string dataUri)
    {
        Id = id;
        BreedName = breedName;
        RoleHint = roleHint;
        AccentColor = accentColor;
        DataUri = dataUri;
    }
}
