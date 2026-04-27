using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;
using AgentPaw.Models;
using AgentPaw.Orchestrator;
using Microsoft.Extensions.Configuration;

namespace AgentPaw.Services;

/// <summary>
/// Flutter/모바일 앱용 REST API 서버 — 포트 47893.
/// Dev 모드 인증: Authorization: Bearer {MobileApi:DevToken}
/// </summary>
public class MobileApiService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly ProjectService _projectService;
    private readonly OrchestratorService _orchestrator;
    private readonly ConfigLoaderService _configLoader;
    private readonly WikiService _wikiService;
    private readonly string _devToken;
    private readonly string _devUserId;

    private const int Port = 47893;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public MobileApiService(
        IDbContextFactory<AgentPawDbContext> dbFactory,
        ProjectService projectService,
        OrchestratorService orchestrator,
        ConfigLoaderService configLoader,
        WikiService wikiService,
        IConfiguration configuration)
    {
        _dbFactory = dbFactory;
        _projectService = projectService;
        _orchestrator = orchestrator;
        _configLoader = configLoader;
        _wikiService = wikiService;
        _devToken = configuration["MobileApi:DevToken"] ?? string.Empty;
        _devUserId = configuration["MobileApi:DevUserId"] ?? string.Empty;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_devToken) || string.IsNullOrWhiteSpace(_devUserId))
        {
            Console.WriteLine("[MobileApi] DevToken 또는 DevUserId가 설정되지 않아 시작하지 않습니다.");
            return;
        }

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://+:{Port}/");
        try { listener.Start(); }
        catch
        {
            listener.Prefixes.Clear();
            listener.Prefixes.Add($"http://localhost:{Port}/");
            listener.Start();
        }

        Console.WriteLine($"[MobileApi] 포트 {Port} 에서 수신 중 (DevUserId={_devUserId})");

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await listener.GetContextAsync(); }
            catch { break; }
            _ = Task.Run(() => HandleAsync(ctx), ct);
        }

        listener.Stop();
    }

    // ─── Auth ──────────────────────────────────────────────────────────────

    private bool CheckAuth(HttpListenerContext ctx)
    {
        var auth = ctx.Request.Headers["Authorization"] ?? string.Empty;
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            auth = auth[7..].Trim();
        if (auth == _devToken) return true;
        WriteJson(ctx, 401, new { error = "Unauthorized" });
        return false;
    }

    // ─── Routing ───────────────────────────────────────────────────────────

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        ctx.Response.Headers.Add("Access-Control-Allow-Headers", "Authorization, Content-Type");
        ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");

        if (ctx.Request.HttpMethod == "OPTIONS")
        {
            ctx.Response.StatusCode = 204;
            ctx.Response.Close();
            return;
        }

        try
        {
            var path = ctx.Request.Url?.AbsolutePath.TrimEnd('/') ?? "/";
            var method = ctx.Request.HttpMethod;

            // /m/health — no auth required
            if (path == "/m/health")
            {
                WriteJson(ctx, 200, new { ok = true, version = GetVersion() });
                return;
            }

            if (!CheckAuth(ctx)) return;

            // /m/me
            if (path == "/m/me" && method == "GET") { await HandleMeAsync(ctx); return; }

            // /m/projects
            if (path == "/m/projects")
            {
                if (method == "GET") { await HandleListProjectsAsync(ctx); return; }
                if (method == "POST") { await HandleCreateProjectAsync(ctx); return; }
            }

            // /m/projects/{id}
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 3 && segments[0] == "m" && segments[1] == "projects")
            {
                var projectId = segments[2];
                var sub = segments.Length >= 4 ? segments[3] : string.Empty;

                if (sub == string.Empty && method == "GET") { await HandleGetProjectAsync(ctx, projectId); return; }
                if (sub == "messages" && method == "GET") { await HandleMessagesAsync(ctx, projectId); return; }
                if (sub == "chat" && method == "POST") { await HandleChatAsync(ctx, projectId); return; }
                if (sub == "personas" && method == "GET") { await HandlePersonasAsync(ctx, projectId); return; }
                if (sub == "wiki" && method == "GET")
                {
                    var wikiId = segments.Length >= 5 ? segments[4] : string.Empty;
                    if (string.IsNullOrEmpty(wikiId)) { await HandleWikiListAsync(ctx, projectId); return; }
                    else { await HandleWikiDetailAsync(ctx, projectId, wikiId); return; }
                }
                if (sub == "timeline" && method == "GET") { await HandleTimelineAsync(ctx, projectId); return; }
            }

            WriteJson(ctx, 404, new { error = "Not found" });
        }
        catch (Exception ex)
        {
            try { WriteJson(ctx, 500, new { error = ex.Message }); } catch { }
        }
    }

    // ─── Handlers ──────────────────────────────────────────────────────────

    private async Task HandleMeAsync(HttpListenerContext ctx)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(_devUserId);
        if (user == null) { WriteJson(ctx, 404, new { error = "User not found" }); return; }
        WriteJson(ctx, 200, new
        {
            userId = user.UserId,
            email = user.Email,
            displayName = user.DisplayName,
            profileImageUrl = user.ProfileImageUrl
        });
    }

    private async Task HandleListProjectsAsync(HttpListenerContext ctx)
    {
        var projects = await _projectService.ListProjectsForUserAsync(_devUserId);
        WriteJson(ctx, 200, projects.Select(p => new
        {
            projectId = p.ProjectId,
            projectName = p.ProjectName,
            description = p.Description,
            status = p.Status,
            createdAt = p.CreatedAt
        }));
    }

    private async Task HandleCreateProjectAsync(HttpListenerContext ctx)
    {
        var body = await ReadBodyAsync(ctx);
        using var doc = JsonDocument.Parse(body.Length > 0 ? body : "{}");
        var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var desc = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : null;
        if (string.IsNullOrWhiteSpace(name)) { WriteJson(ctx, 400, new { error = "name is required" }); return; }
        var project = await _projectService.CreateProjectAsync(_devUserId, name, desc);
        WriteJson(ctx, 201, new
        {
            projectId = project.ProjectId,
            projectName = project.ProjectName,
            description = project.Description,
            status = project.Status,
            createdAt = project.CreatedAt
        });
    }

    private async Task HandleGetProjectAsync(HttpListenerContext ctx, string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var project = await db.Projects.FindAsync(projectId);
        if (project == null) { WriteJson(ctx, 404, new { error = "Project not found" }); return; }
        WriteJson(ctx, 200, new
        {
            projectId = project.ProjectId,
            projectName = project.ProjectName,
            description = project.Description,
            gitRepoPath = project.GitRepoPath,
            askUserEnabled = project.AskUserEnabled,
            googleDocId = project.GoogleDocId,
            status = project.Status,
            createdAt = project.CreatedAt,
            updatedAt = project.UpdatedAt
        });
    }

    private async Task HandleMessagesAsync(HttpListenerContext ctx, string projectId)
    {
        var query = ctx.Request.QueryString;
        var limit = int.TryParse(query["limit"], out var l) ? Math.Clamp(l, 1, 200) : 50;
        var before = query["before"] ?? string.Empty;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var q = db.EventLogs.AsNoTracking()
            .Where(e => e.ProjectId == projectId && !e.IsDeleted
                && (e.EventType == "USER_MESSAGE" || e.EventType == "AI_RESPONSE"
                    || e.EventType == "PM_RESPONSE" || e.EventType == "PM_REPORT"
                    || e.EventType == "PM_INTERVENTION"));

        if (!string.IsNullOrEmpty(before))
        {
            var pivot = await db.EventLogs.AsNoTracking()
                .Where(e => e.EventId == before).Select(e => e.CreatedAt).FirstOrDefaultAsync();
            if (pivot != default) q = q.Where(e => e.CreatedAt < pivot);
        }

        var events = await q.OrderByDescending(e => e.CreatedAt).Take(limit)
            .OrderBy(e => e.CreatedAt).ToListAsync();

        WriteJson(ctx, 200, events.Select(e => new
        {
            eventId = e.EventId,
            eventType = e.EventType,
            payload = e.Payload,
            modelUsed = e.ModelUsed,
            createdAt = e.CreatedAt
        }));
    }

    private async Task HandleChatAsync(HttpListenerContext ctx, string projectId)
    {
        var body = await ReadBodyAsync(ctx);
        using var doc = JsonDocument.Parse(body.Length > 0 ? body : "{}");
        var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(message)) { WriteJson(ctx, 400, new { error = "message is required" }); return; }

        List<string>? teamIds = null;
        if (doc.RootElement.TryGetProperty("teamIds", out var ti) && ti.ValueKind == JsonValueKind.Array)
            teamIds = ti.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList();

        string? teamMode = doc.RootElement.TryGetProperty("teamMode", out var tm) ? tm.GetString() : null;
        string? forcePersonaId = doc.RootElement.TryGetProperty("forcePersonaId", out var fp) ? fp.GetString() : null;

        // 이전 대화는 payload에서 직접 받거나 생략 (모바일에서 관리)
        var turns = new List<object>();
        var input = new OrchestratorInput
        {
            ProjectId = projectId,
            UserId = _devUserId,
            Message = message,
            ForcePersonaId = forcePersonaId,
            TeamPersonaIds = teamIds?.Count >= 2 ? teamIds : null,
            TeamMode = teamMode ?? "panel",
            AskUserEnabled = false // 모바일에서는 자동 판단
        };

        var progress = new Progress<AgentTurn>(turn =>
        {
            if (!turn.IsStreamingPreview)
                turns.Add(new
                {
                    personaId = turn.PersonaId,
                    personaLabel = turn.PersonaLabel,
                    personaAvatar = turn.PersonaAvatar,
                    content = turn.Content,
                    modelUsed = turn.ModelUsed,
                    isPm = turn.IsPm,
                    turnIndex = turn.TurnIndex,
                    isStreamingPreview = false
                });
        });

        var result = await _orchestrator.RunPipelineAsync(input, progress);

        WriteJson(ctx, 200, new
        {
            eventId = result.EventId,
            personaId = result.PersonaId,
            personaLabel = result.PersonaLabel,
            content = result.Content,
            turns
        });
    }

    private async Task HandlePersonasAsync(HttpListenerContext ctx, string projectId)
    {
        var personas = await _configLoader.ListPersonasAsync(projectId);
        WriteJson(ctx, 200, personas.Select(p => new
        {
            personaId = p.PersonaId,
            name = p.Name,
            label = p.Label,
            description = p.Description,
            avatar = p.Avatar,
            icon = p.Icon,
            color = p.Color,
            isPm = p.IsPm,
            primaryModel = p.PrimaryModel
        }));
    }

    private async Task HandleWikiListAsync(HttpListenerContext ctx, string projectId)
    {
        var wikis = await _wikiService.ListWikisAsync(projectId);
        WriteJson(ctx, 200, wikis.Select(w => new
        {
            wikiId = w.WikiId,
            category = w.Category,
            title = w.Title,
            version = w.Version,
            updatedAt = w.UpdatedAt
        }));
    }

    private async Task HandleWikiDetailAsync(HttpListenerContext ctx, string projectId, string wikiId)
    {
        var wiki = await _wikiService.GetWikiAsync(wikiId);
        if (wiki == null || wiki.ProjectId != projectId) { WriteJson(ctx, 404, new { error = "Not found" }); return; }
        WriteJson(ctx, 200, new
        {
            wikiId = wiki.WikiId,
            category = wiki.Category,
            title = wiki.Title,
            content = wiki.Content,
            version = wiki.Version,
            sourceEventId = wiki.SourceEventId,
            createdAt = wiki.CreatedAt,
            updatedAt = wiki.UpdatedAt
        });
    }

    private async Task HandleTimelineAsync(HttpListenerContext ctx, string projectId)
    {
        var query = ctx.Request.QueryString;
        var limit = int.TryParse(query["limit"], out var l) ? Math.Clamp(l, 1, 100) : 30;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var events = await db.EventLogs.AsNoTracking()
            .Where(e => e.ProjectId == projectId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        WriteJson(ctx, 200, events.Select(e => new
        {
            eventId = e.EventId,
            eventType = e.EventType,
            modelUsed = e.ModelUsed,
            triggeredBy = e.TriggeredBy,
            createdAt = e.CreatedAt
        }));
    }

    // ─── Helpers ───────────────────────────────────────────────────────────

    private static void WriteJson(HttpListenerContext ctx, int statusCode, object data)
    {
        var json = JsonSerializer.Serialize(data, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes);
        ctx.Response.Close();
    }

    private static async Task<string> ReadBodyAsync(HttpListenerContext ctx)
    {
        using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static string GetVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";
    }
}
