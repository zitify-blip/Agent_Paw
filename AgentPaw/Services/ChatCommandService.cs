using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;

namespace AgentPaw.Services;

public class ChatCommandService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly WikiService _wiki;

    public ChatCommandService(IDbContextFactory<AgentPawDbContext> dbFactory, WikiService wiki)
    {
        _dbFactory = dbFactory;
        _wiki = wiki;
    }

    // Space Link 관리
    public async Task<ChatSpaceLink?> FindLinkBySpaceNameAsync(string spaceName)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ChatSpaceLinks.FirstOrDefaultAsync(l => l.SpaceName == spaceName);
    }

    public async Task<ChatSpaceLink> UpsertSpaceLinkAsync(string spaceName, string displayName, bool enabled, string platform = "google")
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var existing = await db.ChatSpaceLinks.FirstOrDefaultAsync(l => l.SpaceName == spaceName && l.Platform == platform);
        if (existing != null)
        {
            existing.SpaceDisplayName = displayName;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            existing = new ChatSpaceLink
            {
                LinkId = Guid.NewGuid().ToString(),
                SpaceName = spaceName,
                SpaceDisplayName = displayName,
                Platform = platform,
                Enabled = enabled,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.ChatSpaceLinks.Add(existing);
        }
        await db.SaveChangesAsync();
        return existing;
    }

    public async Task<List<ChatSpaceLink>> ListLinksAsync(string? platform = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.ChatSpaceLinks.AsQueryable();
        if (platform != null)
            query = query.Where(l => l.Platform == platform);
        return await query.OrderBy(l => l.SpaceDisplayName).ToListAsync();
    }

    public async Task SetLinkEnabledAsync(string linkId, bool enabled)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var link = await db.ChatSpaceLinks.FindAsync(linkId);
        if (link != null)
        {
            link.Enabled = enabled;
            link.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    public async Task DeleteLinkAsync(string linkId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        // 세션 상태 cascade 삭제
        var sessions = await db.ChatSessionStates.Where(s => s.LinkId == linkId).ToListAsync();
        db.ChatSessionStates.RemoveRange(sessions);
        var link = await db.ChatSpaceLinks.FindAsync(linkId);
        if (link != null) db.ChatSpaceLinks.Remove(link);
        await db.SaveChangesAsync();
    }

    // 세션 상태 관리
    public async Task<ChatSessionState> GetOrCreateSessionAsync(string linkId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var session = await db.ChatSessionStates.FirstOrDefaultAsync(s => s.LinkId == linkId);
        if (session != null) return session;

        session = new ChatSessionState
        {
            SessionId = Guid.NewGuid().ToString(),
            LinkId = linkId,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.ChatSessionStates.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    // 명령어 실행
    public async Task<CommandResult> ExecuteCommandAsync(string linkId, string rawText)
    {
        var text = rawText.Trim();
        if (!text.StartsWith('/'))
            return new CommandResult { IsCommand = false, Message = text };

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLowerInvariant();
        var args = parts.Length > 1 ? parts[1..] : [];

        return command switch
        {
            "/help" => new CommandResult { IsCommand = true, Reply = GetHelpText() },
            "/projects" => await HandleProjectsCommand(linkId),
            "/project" => await HandleProjectCommand(linkId, args),
            "/personas" => await HandlePersonasCommand(linkId),
            "/persona" => await HandlePersonaCommand(linkId, args),
            "/reset" => await HandleResetCommand(linkId),
            "/wiki" => await HandleWikiCommand(linkId, args),
            _ => new CommandResult { IsCommand = true, Reply = $"알 수 없는 명령어: {command}\n`/help`로 사용 가능한 명령어를 확인하세요." }
        };
    }

    private static string GetHelpText() =>
        """
        *Agent Paw 명령어*

        `/help` — 명령어 목록
        `/projects` — 활성 프로젝트 목록
        `/project <이름>` — 프로젝트 전환
        `/project current` — 현재 프로젝트 확인
        `/personas` — 페르소나 목록
        `/persona <이름|auto>` — 페르소나 지정/자동
        `/persona current` — 현재 페르소나 확인
        `/wiki [키워드|ADR|SPEC|TROUBLE]` — 위키 검색·목록 (인자 없으면 최근 10건)
        `/reset` — 세션 초기화

        명령어 없이 메시지를 보내면 AI가 응답합니다.
        """;

    private async Task<CommandResult> HandleProjectsCommand(string linkId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var projects = await db.Projects.Where(p => p.Status == "ACTIVE").ToListAsync();
        if (projects.Count == 0)
            return new CommandResult { IsCommand = true, Reply = "활성 프로젝트가 없습니다." };

        var session = await GetOrCreateSessionAsync(linkId);
        var lines = projects.Select(p =>
            p.ProjectId == session.ActiveProjectId ? $"▸ *{p.ProjectName}* (현재)" : $"  {p.ProjectName}");
        return new CommandResult { IsCommand = true, Reply = $"*프로젝트 목록*\n{string.Join('\n', lines)}" };
    }

    private async Task<CommandResult> HandleProjectCommand(string linkId, string[] args)
    {
        var session = await GetOrCreateSessionAsync(linkId);

        if (args.Length == 0 || args[0].Equals("current", StringComparison.OrdinalIgnoreCase))
        {
            if (session.ActiveProjectId == null)
                return new CommandResult { IsCommand = true, Reply = "활성 프로젝트가 설정되지 않았습니다.\n`/project <이름>` 으로 설정하세요." };

            await using var db = await _dbFactory.CreateDbContextAsync();
            var project = await db.Projects.FindAsync(session.ActiveProjectId);
            return new CommandResult { IsCommand = true, Reply = $"현재 프로젝트: *{project?.ProjectName ?? session.ActiveProjectId}*" };
        }

        var query = string.Join(' ', args).ToLowerInvariant();
        await using var dbSearch = await _dbFactory.CreateDbContextAsync();
        var match = await dbSearch.Projects
            .Where(p => p.Status == "ACTIVE" && p.ProjectName.ToLower().Contains(query))
            .FirstOrDefaultAsync();

        if (match == null)
            return new CommandResult { IsCommand = true, Reply = $"'{string.Join(' ', args)}' 프로젝트를 찾을 수 없습니다." };

        // 세션 업데이트
        await using var dbUpdate = await _dbFactory.CreateDbContextAsync();
        var s = await dbUpdate.ChatSessionStates.FirstOrDefaultAsync(x => x.LinkId == linkId);
        if (s != null)
        {
            s.ActiveProjectId = match.ProjectId;
            s.ActivePersonaId = null; // 프로젝트 변경 시 페르소나 리셋
            s.UpdatedAt = DateTimeOffset.UtcNow;
            await dbUpdate.SaveChangesAsync();
        }

        return new CommandResult { IsCommand = true, Reply = $"프로젝트를 *{match.ProjectName}* 으로 전환했습니다." };
    }

    private async Task<CommandResult> HandlePersonasCommand(string linkId)
    {
        var session = await GetOrCreateSessionAsync(linkId);
        if (session.ActiveProjectId == null)
            return new CommandResult { IsCommand = true, Reply = "프로젝트를 먼저 설정하세요. `/projects`" };

        await using var db = await _dbFactory.CreateDbContextAsync();
        var linkedIds = await db.ProjectPersonas
            .Where(pp => pp.ProjectId == session.ActiveProjectId)
            .Select(pp => pp.PersonaId)
            .ToListAsync();
        var personas = await db.Personas
            .Where(p => linkedIds.Contains(p.PersonaId))
            .OrderBy(p => p.SortOrder)
            .ToListAsync();
        if (personas.Count == 0)
            return new CommandResult { IsCommand = true, Reply = "페르소나가 없습니다." };

        var lines = personas.Select(p =>
            p.PersonaId == session.ActivePersonaId ? $"▸ *{p.Label}* (고정)" : $"  {p.Label}");
        var mode = session.ActivePersonaId == null ? "(자동 분류)" : "";
        return new CommandResult { IsCommand = true, Reply = $"*페르소나 목록* {mode}\n{string.Join('\n', lines)}" };
    }

    private async Task<CommandResult> HandlePersonaCommand(string linkId, string[] args)
    {
        var session = await GetOrCreateSessionAsync(linkId);
        if (session.ActiveProjectId == null)
            return new CommandResult { IsCommand = true, Reply = "프로젝트를 먼저 설정하세요." };

        if (args.Length == 0 || args[0].Equals("current", StringComparison.OrdinalIgnoreCase))
        {
            if (session.ActivePersonaId == null)
                return new CommandResult { IsCommand = true, Reply = "페르소나: *자동 분류*" };

            await using var db = await _dbFactory.CreateDbContextAsync();
            var persona = await db.Personas.FindAsync(session.ActivePersonaId);
            return new CommandResult { IsCommand = true, Reply = $"페르소나: *{persona?.Label ?? session.ActivePersonaId}*" };
        }

        if (args[0].Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            await using var dbUpdate = await _dbFactory.CreateDbContextAsync();
            var s = await dbUpdate.ChatSessionStates.FirstOrDefaultAsync(x => x.LinkId == linkId);
            if (s != null)
            {
                s.ActivePersonaId = null;
                s.UpdatedAt = DateTimeOffset.UtcNow;
                await dbUpdate.SaveChangesAsync();
            }
            return new CommandResult { IsCommand = true, Reply = "페르소나를 *자동 분류*로 전환했습니다." };
        }

        var query = string.Join(' ', args).ToLowerInvariant();
        await using var dbSearch = await _dbFactory.CreateDbContextAsync();
        var match = await dbSearch.Personas
            .Where(p => p.ProjectId == session.ActiveProjectId && p.Label.ToLower().Contains(query))
            .FirstOrDefaultAsync();

        if (match == null)
            return new CommandResult { IsCommand = true, Reply = $"'{string.Join(' ', args)}' 페르소나를 찾을 수 없습니다." };

        await using var dbSet = await _dbFactory.CreateDbContextAsync();
        var sess = await dbSet.ChatSessionStates.FirstOrDefaultAsync(x => x.LinkId == linkId);
        if (sess != null)
        {
            sess.ActivePersonaId = match.PersonaId;
            sess.UpdatedAt = DateTimeOffset.UtcNow;
            await dbSet.SaveChangesAsync();
        }

        return new CommandResult { IsCommand = true, Reply = $"페르소나를 *{match.Label}* 으로 고정했습니다." };
    }

    private async Task<CommandResult> HandleWikiCommand(string linkId, string[] args)
    {
        var session = await GetOrCreateSessionAsync(linkId);
        if (session.ActiveProjectId == null)
            return new CommandResult { IsCommand = true, Reply = "프로젝트를 먼저 설정하세요. `/projects`" };

        const int MaxResults = 10;
        List<WikiDocument> hits;
        string? categoryFilter = null;
        string? keyword = null;

        if (args.Length > 0)
        {
            // 첫 인자가 카테고리 단축어면 카테고리 필터로 해석한다
            var first = args[0].Trim().ToUpperInvariant();
            if (first is "ADR" or "SPEC" or "TROUBLE")
            {
                categoryFilter = $"WIKI_{first}";
                if (args.Length > 1) keyword = string.Join(' ', args[1..]).Trim();
            }
            else
            {
                keyword = string.Join(' ', args).Trim();
            }
        }

        if (!string.IsNullOrEmpty(keyword))
            hits = await _wiki.SearchWikisAsync(session.ActiveProjectId, keyword);
        else
            hits = await _wiki.ListWikisAsync(session.ActiveProjectId);

        if (categoryFilter != null)
            hits = hits.Where(w => w.Category == categoryFilter).ToList();

        if (hits.Count == 0)
        {
            var header = !string.IsNullOrEmpty(keyword)
                ? $"'{keyword}' 에 해당하는 위키가 없습니다."
                : "이 프로젝트에 저장된 위키가 없습니다.";
            return new CommandResult { IsCommand = true, Reply = header };
        }

        var top = hits.Take(MaxResults).ToList();
        var lines = top.Select((w, i) =>
        {
            var cat = w.Category switch
            {
                "WIKI_ADR" => "ADR",
                "WIKI_SPEC" => "SPEC",
                "WIKI_TROUBLE" => "TROUBLE",
                _ => "DOC"
            };
            var date = w.UpdatedAt.LocalDateTime.ToString("yyyy-MM-dd");
            return $"{i + 1}. [{cat}] *{w.Title}* — {date}";
        });

        var title = !string.IsNullOrEmpty(keyword)
            ? $"*위키 검색 결과* (키워드: {keyword}, 총 {hits.Count}건)"
            : $"*최근 위키* (총 {hits.Count}건)";

        var reply = $"{title}\n{string.Join('\n', lines)}";
        if (hits.Count > MaxResults)
            reply += $"\n\n(상위 {MaxResults}건만 표시 — 키워드로 좁혀보세요)";

        return new CommandResult { IsCommand = true, Reply = reply };
    }

    private async Task<CommandResult> HandleResetCommand(string linkId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var session = await db.ChatSessionStates.FirstOrDefaultAsync(s => s.LinkId == linkId);
        if (session != null)
        {
            db.ChatSessionStates.Remove(session);
            await db.SaveChangesAsync();
        }
        return new CommandResult { IsCommand = true, Reply = "세션이 초기화되었습니다." };
    }
}

public class CommandResult
{
    public bool IsCommand { get; set; }
    public string? Reply { get; set; }
    public string? Message { get; set; } // 명령어가 아닌 일반 메시지
}
