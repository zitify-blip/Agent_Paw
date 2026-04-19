using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

public class ConfigLoaderService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;

    // 시스템 프롬프트 구성 비용(프로젝트 지침 + 페르소나 지침 JOIN 조회 + StringBuilder 합성)을 턴마다 반복하지 않기 위한 캐시.
    // Key: (personaId, projectId), TTL: 30초 — 지침 편집 누락에 대한 안전망. 편집 시점에는 InvalidateAll()을 명시 호출한다.
    private static readonly ConcurrentDictionary<(string personaId, string projectId), CachedConfig> _configCache = new();
    private static readonly TimeSpan ConfigCacheTtl = TimeSpan.FromSeconds(30);

    private sealed record CachedConfig(PersonaConfig Config, DateTime ExpiresUtc);

    private const string LanguageDirective = """
        [언어 정책]
        사용자가 입력한 언어와 동일한 언어로 응답한다.
        - 한국어 입력 → 한국어 응답
        - English input → English response
        - 日本語入力 → 日本語応答
        """;

    private const string PriorityDirective = """
        [지침 우선순위]
        1순위: 프로젝트 지침(PROJECT INSTRUCTIONS) — 반드시 준수한다.
        2순위: 페르소나 지침(PERSONA INSTRUCTIONS) — 1순위와 충돌하지 않는 범위에서 따른다.
        아무리 페르소나의 고유 성향이 강하더라도 프로젝트 지침이 우선한다. 충돌 시 프로젝트 지침을 따르고, 필요하면 PM에게 handoff로 확인을 요청한다.
        """;

    public ConfigLoaderService(IDbContextFactory<AgentPawDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>
    /// 지침/페르소나가 편집됐을 때 호출해 캐시된 시스템 프롬프트를 폐기한다.
    /// 편집 경로가 많아 개별 key 무효화보다 전체 일괄 폐기가 누락 위험을 줄인다.
    /// </summary>
    public void InvalidateAll() => _configCache.Clear();

    public async Task<PersonaConfig> GetPersonaConfigAsync(string personaId, string projectId)
    {
        var cacheKey = (personaId, projectId);
        if (_configCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresUtc > DateTime.UtcNow)
            return cached.Config;

        await using var db = await _dbFactory.CreateDbContextAsync();

        var persona = await db.Personas.FindAsync(personaId);
        if (persona == null)
        {
            return new PersonaConfig
            {
                PersonaId = personaId,
                Name = "DEFAULT",
                SystemPrompt = LanguageDirective,
                PrimaryModel = "claude-sonnet",
                FallbackModel = "gemini-flash",
                Temperature = 0.7f,
                MaxTokens = 4096
            };
        }

        // 프로젝트 지침 (1순위)
        var projectInstructions = await (
            from pi in db.ProjectInstructions.AsNoTracking()
            join f in db.InstructionFiles.AsNoTracking() on pi.FileId equals f.FileId
            where pi.ProjectId == projectId
            orderby f.Name
            select new { f.Name, f.Content }
        ).ToListAsync();

        // 페르소나 지침 (2순위) — persona_instruction 링크 + persona.Instructions 프리폼 필드
        var personaLinkedInstructions = await (
            from pi in db.PersonaInstructions.AsNoTracking()
            join f in db.InstructionFiles.AsNoTracking() on pi.FileId equals f.FileId
            where pi.PersonaId == personaId
            orderby f.Name
            select new { f.Name, f.Content }
        ).ToListAsync();

        // 시스템 프롬프트 조합: 역할 정의 + 우선순위 선언 + 프로젝트 지침 + 페르소나 지침 + 언어 정책
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(persona.SystemPrompt))
            parts.Add("[역할 정의]\n" + persona.SystemPrompt.Trim());

        parts.Add(PriorityDirective);

        if (projectInstructions.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[PROJECT INSTRUCTIONS — 1순위, 반드시 준수]");
            foreach (var f in projectInstructions)
            {
                if (string.IsNullOrWhiteSpace(f.Content)) continue;
                sb.AppendLine();
                sb.AppendLine($"--- {f.Name} ---");
                sb.Append(f.Content.TrimEnd());
                sb.AppendLine();
            }
            parts.Add(sb.ToString().TrimEnd());
        }

        var personaHasContent =
            !string.IsNullOrWhiteSpace(persona.Instructions) ||
            personaLinkedInstructions.Any(f => !string.IsNullOrWhiteSpace(f.Content));
        if (personaHasContent)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[PERSONA INSTRUCTIONS — 2순위, 프로젝트 지침과 충돌 시 프로젝트 지침 우선]");
            if (!string.IsNullOrWhiteSpace(persona.Instructions))
            {
                sb.AppendLine();
                sb.AppendLine("--- (inline) ---");
                sb.Append(persona.Instructions.TrimEnd());
                sb.AppendLine();
            }
            foreach (var f in personaLinkedInstructions)
            {
                if (string.IsNullOrWhiteSpace(f.Content)) continue;
                sb.AppendLine();
                sb.AppendLine($"--- {f.Name} ---");
                sb.Append(f.Content.TrimEnd());
                sb.AppendLine();
            }
            parts.Add(sb.ToString().TrimEnd());
        }

        parts.Add(LanguageDirective);

        var config = new PersonaConfig
        {
            PersonaId = persona.PersonaId,
            Name = persona.Name,
            Label = persona.Label,
            SystemPrompt = string.Join("\n\n", parts),
            PrimaryModel = persona.PrimaryModel,
            FallbackModel = persona.FallbackModel,
            Temperature = persona.Temperature,
            MaxTokens = persona.MaxTokens,
            Keywords = persona.Keywords,
            Avatar = persona.Avatar
        };

        _configCache[cacheKey] = new CachedConfig(config, DateTime.UtcNow.Add(ConfigCacheTtl));
        return config;
    }

    public async Task<List<Persona>> ListPersonasAsync(string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var linkedIds = await db.ProjectPersonas
            .Where(pp => pp.ProjectId == projectId)
            .Select(pp => pp.PersonaId)
            .ToListAsync();
        return await db.Personas
            .Where(p => linkedIds.Contains(p.PersonaId))
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
    }
}

public class PersonaConfig
{
    public string PersonaId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string PrimaryModel { get; set; } = "claude-sonnet";
    public string? FallbackModel { get; set; }
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 4096;
    public string Keywords { get; set; } = string.Empty;
    public string Avatar { get; set; } = string.Empty;
}
