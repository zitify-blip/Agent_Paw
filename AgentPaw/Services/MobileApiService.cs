using System.Net;
using System.Net.Sockets;
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
/// TcpListener(Any) 사용으로 관리자 권한 없이 외부 접속 허용.
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
    private static readonly Dictionary<string, string> CorsHeaders = new()
    {
        ["Access-Control-Allow-Origin"] = "*",
        ["Access-Control-Allow-Headers"] = "Authorization, Content-Type",
        ["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS"
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

        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        Console.WriteLine($"[MobileApi] 포트 {Port} 에서 수신 중 (DevUserId={_devUserId})");
        Console.WriteLine($"[MobileApi] 방화벽 미설정 시: netsh advfirewall firewall add rule name=\"AgentPaw MobileAPI\" dir=in action=allow protocol=TCP localport={Port}");

        using var reg = ct.Register(() => { try { listener.Stop(); } catch { } });

        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(); }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (ct.IsCancellationRequested) { break; }
            catch { continue; }
            _ = Task.Run(() => HandleClientAsync(client), ct);
        }

        listener.Stop();
    }

    // ─── TCP → HTTP 파싱 ───────────────────────────────────────────────────

    private async Task HandleClientAsync(TcpClient tcpClient)
    {
        try
        {
            using var client = tcpClient;
            client.ReceiveTimeout = 10_000;
            var stream = client.GetStream();
            var req = await ReadRequestAsync(stream);
            if (req == null) return;
            await RouteAsync(stream, req);
        }
        catch { }
    }

    private static async Task<HttpReq?> ReadRequestAsync(NetworkStream stream)
    {
        var buf = new byte[8192];
        var received = new List<byte>(4096);

        // \r\n\r\n 을 찾을 때까지 읽기
        while (true)
        {
            var n = await stream.ReadAsync(buf.AsMemory(0, buf.Length));
            if (n == 0) return null;
            received.AddRange(buf[..n]);

            for (var i = 0; i <= received.Count - 4; i++)
            {
                if (received[i] != '\r' || received[i + 1] != '\n' ||
                    received[i + 2] != '\r' || received[i + 3] != '\n') continue;

                var hdrText = Encoding.UTF8.GetString(received.GetRange(0, i + 4).ToArray());
                var extra   = received.GetRange(i + 4, received.Count - i - 4).ToArray();

                var lines = hdrText.Split("\r\n", StringSplitOptions.None);
                if (lines.Length == 0) return null;

                var parts = lines[0].Split(' ');
                if (parts.Length < 2) return null;
                var method = parts[0];
                var full   = parts[1];
                var qi     = full.IndexOf('?');
                var path   = (qi >= 0 ? full[..qi] : full).TrimEnd('/');
                if (string.IsNullOrEmpty(path)) path = "/";
                var qs = qi >= 0 ? full[(qi + 1)..] : "";

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var j = 1; j < lines.Length; j++)
                {
                    var ci = lines[j].IndexOf(':');
                    if (ci < 0) continue;
                    headers[lines[j][..ci].Trim()] = lines[j][(ci + 1)..].Trim();
                }

                byte[] body = [];
                if (headers.TryGetValue("Content-Length", out var clStr)
                    && int.TryParse(clStr, out var cl) && cl > 0)
                {
                    body = new byte[cl];
                    var copyLen = Math.Min(extra.Length, cl);
                    Array.Copy(extra, body, copyLen);
                    var offset = copyLen;
                    while (offset < cl)
                    {
                        var r = await stream.ReadAsync(body.AsMemory(offset, cl - offset));
                        if (r == 0) break;
                        offset += r;
                    }
                }

                return new HttpReq(method, path, qs, headers, body);
            }

            if (received.Count > 65_536) return null;
        }
    }

    // ─── Auth ──────────────────────────────────────────────────────────────

    private bool CheckAuth(HttpReq req)
    {
        var auth = req.Headers.TryGetValue("Authorization", out var v) ? v : "";
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            auth = auth[7..].Trim();
        return auth == _devToken;
    }

    // ─── Routing ───────────────────────────────────────────────────────────

    private async Task RouteAsync(NetworkStream stream, HttpReq req)
    {
        if (req.Method == "OPTIONS")
        {
            await WriteResponseAsync(stream, 204, null, CorsHeaders);
            return;
        }

        try
        {
            var path   = req.Path;
            var method = req.Method;

            if (path == "/m/health" && method == "GET")
            {
                await WriteJsonAsync(stream, 200, new { ok = true, version = GetVersion() });
                return;
            }

            if (!CheckAuth(req)) { await WriteJsonAsync(stream, 401, new { error = "Unauthorized" }); return; }

            if (path == "/m/me" && method == "GET") { await HandleMeAsync(stream); return; }

            if (path == "/m/projects")
            {
                if (method == "GET")  { await HandleListProjectsAsync(stream); return; }
                if (method == "POST") { await HandleCreateProjectAsync(stream, req); return; }
            }

            var segs = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length >= 3 && segs[0] == "m" && segs[1] == "projects")
            {
                var projectId = segs[2];
                var sub = segs.Length >= 4 ? segs[3] : "";

                if (sub == "" && method == "GET")           { await HandleGetProjectAsync(stream, projectId); return; }
                if (sub == "messages" && method == "GET")   { await HandleMessagesAsync(stream, req, projectId); return; }
                if (sub == "chat" && method == "POST")      { await HandleChatAsync(stream, req, projectId); return; }
                if (sub == "personas" && method == "GET")   { await HandlePersonasAsync(stream, projectId); return; }
                if (sub == "timeline" && method == "GET")   { await HandleTimelineAsync(stream, req, projectId); return; }
                if (sub == "wiki" && method == "GET")
                {
                    var wikiId = segs.Length >= 5 ? segs[4] : "";
                    if (string.IsNullOrEmpty(wikiId)) { await HandleWikiListAsync(stream, projectId); return; }
                    else { await HandleWikiDetailAsync(stream, projectId, wikiId); return; }
                }
            }

            await WriteJsonAsync(stream, 404, new { error = "Not found" });
        }
        catch (Exception ex)
        {
            try { await WriteJsonAsync(stream, 500, new { error = ex.Message }); } catch { }
        }
    }

    // ─── Handlers ──────────────────────────────────────────────────────────

    private async Task HandleMeAsync(NetworkStream stream)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.FindAsync(_devUserId);
        if (user == null) { await WriteJsonAsync(stream, 404, new { error = "User not found" }); return; }
        await WriteJsonAsync(stream, 200, new
        {
            userId = user.UserId,
            email = user.Email,
            displayName = user.DisplayName,
            profileImageUrl = user.ProfileImageUrl
        });
    }

    private async Task HandleListProjectsAsync(NetworkStream stream)
    {
        var projects = await _projectService.ListProjectsForUserAsync(_devUserId);
        await WriteJsonAsync(stream, 200, projects.Select(p => new
        {
            projectId = p.ProjectId,
            projectName = p.ProjectName,
            description = p.Description,
            status = p.Status,
            createdAt = p.CreatedAt
        }));
    }

    private async Task HandleCreateProjectAsync(NetworkStream stream, HttpReq req)
    {
        var text = Encoding.UTF8.GetString(req.Body);
        using var doc = JsonDocument.Parse(text.Length > 0 ? text : "{}");
        var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var desc = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : null;
        if (string.IsNullOrWhiteSpace(name)) { await WriteJsonAsync(stream, 400, new { error = "name is required" }); return; }
        var project = await _projectService.CreateProjectAsync(_devUserId, name, desc);
        await WriteJsonAsync(stream, 201, new
        {
            projectId = project.ProjectId,
            projectName = project.ProjectName,
            description = project.Description,
            status = project.Status,
            createdAt = project.CreatedAt
        });
    }

    private async Task HandleGetProjectAsync(NetworkStream stream, string projectId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var project = await db.Projects.FindAsync(projectId);
        if (project == null) { await WriteJsonAsync(stream, 404, new { error = "Project not found" }); return; }
        await WriteJsonAsync(stream, 200, new
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

    private async Task HandleMessagesAsync(NetworkStream stream, HttpReq req, string projectId)
    {
        var q = ParseQuery(req.QueryString);
        var limit  = int.TryParse(q.GetValueOrDefault("limit"), out var l) ? Math.Clamp(l, 1, 200) : 50;
        var before = q.GetValueOrDefault("before") ?? "";

        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.EventLogs.AsNoTracking()
            .Where(e => e.ProjectId == projectId && !e.IsDeleted
                && (e.EventType == "USER_MESSAGE" || e.EventType == "AI_RESPONSE"
                    || e.EventType == "PM_RESPONSE" || e.EventType == "PM_REPORT"
                    || e.EventType == "PM_INTERVENTION"));

        if (!string.IsNullOrEmpty(before))
        {
            var pivot = await db.EventLogs.AsNoTracking()
                .Where(e => e.EventId == before).Select(e => e.CreatedAt).FirstOrDefaultAsync();
            if (pivot != default) query = query.Where(e => e.CreatedAt < pivot);
        }

        var events = await query.OrderByDescending(e => e.CreatedAt).Take(limit)
            .OrderBy(e => e.CreatedAt).ToListAsync();

        await WriteJsonAsync(stream, 200, events.Select(e => new
        {
            eventId   = e.EventId,
            eventType = e.EventType,
            payload   = e.Payload,
            modelUsed = e.ModelUsed,
            createdAt = e.CreatedAt
        }));
    }

    private async Task HandleChatAsync(NetworkStream stream, HttpReq req, string projectId)
    {
        var text = Encoding.UTF8.GetString(req.Body);
        using var doc = JsonDocument.Parse(text.Length > 0 ? text : "{}");
        var message = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(message)) { await WriteJsonAsync(stream, 400, new { error = "message is required" }); return; }

        List<string>? teamIds = null;
        if (doc.RootElement.TryGetProperty("teamIds", out var ti) && ti.ValueKind == JsonValueKind.Array)
            teamIds = ti.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList();

        string? teamMode       = doc.RootElement.TryGetProperty("teamMode",       out var tm) ? tm.GetString() : null;
        string? forcePersonaId = doc.RootElement.TryGetProperty("forcePersonaId", out var fp) ? fp.GetString() : null;

        var turns = new List<object>();
        var input = new OrchestratorInput
        {
            ProjectId      = projectId,
            UserId         = _devUserId,
            Message        = message,
            ForcePersonaId = forcePersonaId,
            TeamPersonaIds = teamIds?.Count >= 2 ? teamIds : null,
            TeamMode       = teamMode ?? "panel",
            AskUserEnabled = false
        };

        var progress = new Progress<AgentTurn>(turn =>
        {
            if (!turn.IsStreamingPreview)
                turns.Add(new
                {
                    personaId        = turn.PersonaId,
                    personaLabel     = turn.PersonaLabel,
                    personaAvatar    = turn.PersonaAvatar,
                    content          = turn.Content,
                    modelUsed        = turn.ModelUsed,
                    isPm             = turn.IsPm,
                    turnIndex        = turn.TurnIndex,
                    isStreamingPreview = false
                });
        });

        var result = await _orchestrator.RunPipelineAsync(input, progress);

        await WriteJsonAsync(stream, 200, new
        {
            eventId      = result.EventId,
            personaId    = result.PersonaId,
            personaLabel = result.PersonaLabel,
            content      = result.Content,
            turns
        });
    }

    private async Task HandlePersonasAsync(NetworkStream stream, string projectId)
    {
        var personas = await _configLoader.ListPersonasAsync(projectId);
        await WriteJsonAsync(stream, 200, personas.Select(p => new
        {
            personaId    = p.PersonaId,
            name         = p.Name,
            label        = p.Label,
            description  = p.Description,
            avatar       = p.Avatar,
            icon         = p.Icon,
            color        = p.Color,
            isPm         = p.IsPm,
            primaryModel = p.PrimaryModel
        }));
    }

    private async Task HandleWikiListAsync(NetworkStream stream, string projectId)
    {
        var wikis = await _wikiService.ListWikisAsync(projectId);
        await WriteJsonAsync(stream, 200, wikis.Select(w => new
        {
            wikiId    = w.WikiId,
            category  = w.Category,
            title     = w.Title,
            version   = w.Version,
            updatedAt = w.UpdatedAt
        }));
    }

    private async Task HandleWikiDetailAsync(NetworkStream stream, string projectId, string wikiId)
    {
        var wiki = await _wikiService.GetWikiAsync(wikiId);
        if (wiki == null || wiki.ProjectId != projectId) { await WriteJsonAsync(stream, 404, new { error = "Not found" }); return; }
        await WriteJsonAsync(stream, 200, new
        {
            wikiId        = wiki.WikiId,
            category      = wiki.Category,
            title         = wiki.Title,
            content       = wiki.Content,
            version       = wiki.Version,
            sourceEventId = wiki.SourceEventId,
            createdAt     = wiki.CreatedAt,
            updatedAt     = wiki.UpdatedAt
        });
    }

    private async Task HandleTimelineAsync(NetworkStream stream, HttpReq req, string projectId)
    {
        var q = ParseQuery(req.QueryString);
        var limit = int.TryParse(q.GetValueOrDefault("limit"), out var l) ? Math.Clamp(l, 1, 100) : 30;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var events = await db.EventLogs.AsNoTracking()
            .Where(e => e.ProjectId == projectId && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        await WriteJsonAsync(stream, 200, events.Select(e => new
        {
            eventId     = e.EventId,
            eventType   = e.EventType,
            modelUsed   = e.ModelUsed,
            triggeredBy = e.TriggeredBy,
            createdAt   = e.CreatedAt
        }));
    }

    // ─── HTTP 응답 헬퍼 ────────────────────────────────────────────────────

    private static async Task WriteJsonAsync(NetworkStream stream, int status, object? data)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, JsonOpts));
        await WriteResponseAsync(stream, status, body, CorsHeaders);
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int status, byte[]? body, Dictionary<string, string>? extra)
    {
        var statusText = status switch
        {
            200 => "OK", 201 => "Created", 204 => "No Content",
            400 => "Bad Request", 401 => "Unauthorized", 404 => "Not Found",
            500 => "Internal Server Error", _ => "Unknown"
        };
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {status} {statusText}\r\nConnection: close\r\n");
        if (body is { Length: > 0 })
        {
            sb.Append("Content-Type: application/json; charset=utf-8\r\n");
            sb.Append($"Content-Length: {body.Length}\r\n");
        }
        else sb.Append("Content-Length: 0\r\n");
        if (extra != null)
            foreach (var (k, v) in extra) sb.Append($"{k}: {v}\r\n");
        sb.Append("\r\n");
        var hdr = Encoding.UTF8.GetBytes(sb.ToString());
        await stream.WriteAsync(hdr);
        if (body is { Length: > 0 }) await stream.WriteAsync(body);
        await stream.FlushAsync();
    }

    private static Dictionary<string, string> ParseQuery(string qs)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(qs)) return result;
        foreach (var pair in qs.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) { result[Uri.UnescapeDataString(pair)] = ""; continue; }
            result[Uri.UnescapeDataString(pair[..eq])] = Uri.UnescapeDataString(pair[(eq + 1)..]);
        }
        return result;
    }

    private static string GetVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "0.0.0";
    }

    private record HttpReq(
        string Method,
        string Path,
        string QueryString,
        Dictionary<string, string> Headers,
        byte[] Body);
}
