using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;

namespace AgentPaw.Services;

public class StatusHttpService
{
    private const int Port = 47892;

    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private readonly WebSocketServerService _wsService;
    private readonly PubSubPullService _pubSubService;
    private readonly SlackSocketModeService _slackService;
    private readonly TelegramPollingService _telegramService;

    private TcpListener? _tcp;
    private CancellationTokenSource? _cts;
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public bool IsRunning { get; private set; }

    public StatusHttpService(
        IDbContextFactory<AgentPawDbContext> dbFactory,
        WebSocketServerService wsService,
        PubSubPullService pubSubService,
        SlackSocketModeService slackService,
        TelegramPollingService telegramService)
    {
        _dbFactory = dbFactory;
        _wsService = wsService;
        _pubSubService = pubSubService;
        _slackService = slackService;
        _telegramService = telegramService;
    }

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _tcp = new TcpListener(IPAddress.Loopback, Port);
        try
        {
            _tcp.Start();
            IsRunning = true;
            _ = AcceptLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            IsRunning = false;
            try
            {
                var log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "agentpaw_status_error.txt");
                System.IO.File.WriteAllText(log, $"{DateTimeOffset.Now}: StatusHttpService.Start() failed\n{ex}");
            }
            catch { }
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _tcp?.Stop(); } catch { }
        _tcp = null;
        _cts?.Dispose();
        _cts = null;
        IsRunning = false;
    }

    // LoginViewModel과 동일한 패턴: ct.Register로 리스너를 Stop시켜 Accept를 깨운다.
    // AcceptTcpClientAsync(CancellationToken) 오버로드는 일부 환경에서 즉시 throw하므로
    // 인자 없는 오버로드를 사용한다.
    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        using var reg = ct.Register(() => { try { _tcp?.Stop(); } catch { } });

        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _tcp!.AcceptTcpClientAsync();
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) when (ct.IsCancellationRequested) { break; }
            catch (SocketException) { continue; }
            catch { continue; }

            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using var tcpClient = client;
            var stream = tcpClient.GetStream();

            // HTTP 요청 읽기
            var buf = new byte[8192];
            var read = await stream.ReadAsync(buf, 0, buf.Length);
            if (read == 0) return;

            var requestLine = Encoding.UTF8.GetString(buf, 0, read).Split('\n')[0];
            var parts = requestLine.Trim().Split(' ');
            var path = parts.Length >= 2 ? parts[1].Split('?')[0] : "/";

            byte[] body;
            string contentType;

            if (path == "/api/status")
            {
                var payload = await BuildPayloadAsync(CancellationToken.None);
                body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOpts));
                contentType = "application/json; charset=utf-8";
            }
            else
            {
                body = Encoding.UTF8.GetBytes(HtmlPage);
                contentType = "text/html; charset=utf-8";
            }

            var header =
                $"HTTP/1.1 200 OK\r\n" +
                $"Content-Type: {contentType}\r\n" +
                $"Content-Length: {body.Length}\r\n" +
                $"Cache-Control: no-store\r\n" +
                $"Connection: close\r\n\r\n";

            var headerBytes = Encoding.UTF8.GetBytes(header);
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
            await stream.WriteAsync(body, 0, body.Length);
            await stream.FlushAsync();
        }
        catch { }
    }

    private async Task<object> BuildPayloadAsync(CancellationToken ct)
    {
        var uptime = DateTimeOffset.UtcNow - _startedAt;

        int userCount = 0, projectCount = 0, sessionCount = 0;
        bool dbOk = false;
        object[] auditRows = [];
        object[] eventRows = [];

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            dbOk = await db.Database.CanConnectAsync(ct);
            if (dbOk)
            {
                userCount    = await db.Users.CountAsync(u => u.IsActive, ct);
                projectCount = await db.Projects.CountAsync(ct);
                sessionCount = await db.AuthTokens.CountAsync(
                    t => t.TokenType == "APP_SESSION" && !t.IsRevoked &&
                         (t.ExpiresAt == null || t.ExpiresAt > DateTimeOffset.UtcNow), ct);

                auditRows = (await db.AuditLogs
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(20)
                    .Select(a => new { a.Action, a.CreatedAt })
                    .ToListAsync(ct))
                    .Cast<object>().ToArray();

                eventRows = (await db.EventLogs
                    .Where(e => !e.IsDeleted)
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(15)
                    .Select(e => new { e.EventType, e.ProjectId, e.TriggeredBy, e.CreatedAt })
                    .ToListAsync(ct))
                    .Cast<object>().ToArray();
            }
        }
        catch { }

        return new
        {
            Timestamp     = DateTimeOffset.UtcNow,
            Version       = GetVersion(),
            Build         = GetBuildConfig(),
            UptimeSeconds = (long)uptime.TotalSeconds,
            UptimeLabel   = FormatUptime(uptime),
            Services = new
            {
                Websocket = new { Running = _wsService.IsRunning, Port = 8765 },
                Pubsub    = new { Running = _pubSubService.IsRunning },
                Slack     = new { Running = _slackService.IsRunning },
                Telegram  = new { Running = _telegramService.IsRunning }
            },
            Database = new
            {
                Connected      = dbOk,
                Users          = userCount,
                Projects       = projectCount,
                ActiveSessions = sessionCount
            },
            RecentAudit  = auditRows,
            RecentEvents = eventRows
        };
    }

    private static string GetVersion() =>
        typeof(StatusHttpService).Assembly.GetName().Version?.ToString(3) ?? "0.4.3";

    private static string GetBuildConfig()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }

    private static string FormatUptime(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)  return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1) return $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Minutes}m {ts.Seconds}s";
    }

    private const string HtmlPage = """
        <!DOCTYPE html>
        <html lang="ko">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <title>Agent Paw — Status</title>
          <style>
            *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
            body {
              background: #0d1117;
              color: #c9d1d9;
              font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
              font-size: 13px;
              min-height: 100vh;
              padding: 28px 32px;
            }
            .header {
              display: flex;
              align-items: center;
              justify-content: space-between;
              margin-bottom: 28px;
              padding-bottom: 18px;
              border-bottom: 1px solid #21262d;
              flex-wrap: wrap;
              gap: 12px;
            }
            .header-left { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
            .app-name { font-size: 20px; font-weight: 700; color: #f0f6fc; letter-spacing: -0.3px; }
            .badge {
              display: inline-block; padding: 2px 9px; border-radius: 20px;
              font-size: 11px; font-weight: 600; letter-spacing: 0.3px;
            }
            .badge-ver   { background: #21262d; color: #8b949e; border: 1px solid #30363d; }
            .badge-debug { background: #1a2535; color: #58a6ff; border: 1px solid #1f6feb; }
            .badge-rel   { background: #1a2e1a; color: #3fb950; border: 1px solid #238636; }
            .header-right { display: flex; align-items: center; gap: 18px; }
            .uptime-txt { color: #8b949e; font-size: 12px; }
            .live-badge {
              display: flex; align-items: center; gap: 6px;
              font-size: 12px; color: #8b949e;
            }
            .live-dot {
              width: 8px; height: 8px; border-radius: 50%; background: #3fb950;
              animation: blink 2s infinite;
            }
            .live-dot.err { background: #f85149; animation: none; }
            @keyframes blink { 0%,100%{opacity:1} 50%{opacity:.3} }
            .section { margin-bottom: 28px; }
            .section-title {
              font-size: 11px; font-weight: 600; text-transform: uppercase;
              letter-spacing: 1px; color: #8b949e; margin-bottom: 12px;
            }
            .cards { display: grid; grid-template-columns: repeat(auto-fill, minmax(155px, 1fr)); gap: 12px; }
            .card {
              background: #161b22; border: 1px solid #21262d;
              border-radius: 8px; padding: 14px 16px;
            }
            .card-label {
              font-size: 11px; font-weight: 600; text-transform: uppercase;
              letter-spacing: 0.5px; color: #8b949e; margin-bottom: 8px;
            }
            .card-val { font-size: 22px; font-weight: 700; color: #f0f6fc; font-variant-numeric: tabular-nums; }
            .svc-row { display: flex; align-items: center; gap: 8px; }
            .dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }
            .dot-on  { background: #3fb950; box-shadow: 0 0 6px #3fb95044; }
            .dot-off { background: #484f58; }
            .svc-txt { font-size: 13px; font-weight: 600; }
            .svc-txt.on  { color: #3fb950; }
            .svc-txt.off { color: #8b949e; }
            .sub-txt { font-size: 11px; color: #484f58; margin-top: 4px; }
            .db-card { border-left: 3px solid transparent; }
            .db-card.ok  { border-left-color: #3fb950; }
            .db-card.err { border-left-color: #f85149; }
            .two-col { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; }
            @media (max-width: 860px) { .two-col { grid-template-columns: 1fr; } }
            table { width: 100%; border-collapse: collapse; background: #161b22; border: 1px solid #21262d; border-radius: 8px; overflow: hidden; }
            th {
              background: #1c2128; color: #8b949e; font-size: 11px; font-weight: 600;
              text-transform: uppercase; letter-spacing: 0.5px;
              padding: 8px 12px; text-align: left; border-bottom: 1px solid #21262d;
            }
            td { padding: 7px 12px; border-bottom: 1px solid #1c2128; color: #c9d1d9; font-size: 12px; }
            tr:last-child td { border-bottom: none; }
            tr:hover td { background: #1c2128; }
            .tag {
              display: inline-block; padding: 1px 7px; border-radius: 4px;
              font-size: 10px; font-weight: 600; letter-spacing: 0.3px; white-space: nowrap;
            }
            .tag-login   { background: #1a2e1a; color: #3fb950; }
            .tag-logout  { background: #21262d; color: #8b949e; }
            .tag-dev     { background: #2a2010; color: #e3b341; }
            .tag-revoke  { background: #2d1111; color: #f85149; }
            .tag-default { background: #21262d; color: #c9d1d9; }
            .evt-type { font-family: Consolas, monospace; font-size: 11px; color: #58a6ff; }
            .t-ago    { color: #484f58; font-size: 11px; white-space: nowrap; }
            .t-dim    { color: #8b949e; font-size: 11px; }
            .empty    { color: #484f58; font-style: italic; }
            footer {
              margin-top: 36px; padding-top: 16px; border-top: 1px solid #21262d;
              text-align: center; font-size: 11px; color: #484f58;
            }
          </style>
        </head>
        <body>

        <div class="header">
          <div class="header-left">
            <span class="app-name">&#x1F43E; Agent Paw</span>
            <span class="badge badge-ver" id="ver">—</span>
            <span class="badge" id="build-badge">—</span>
          </div>
          <div class="header-right">
            <span class="uptime-txt">&#x23F1; <span id="uptime">—</span></span>
            <span class="live-badge"><span class="live-dot" id="live-dot"></span><span id="live-txt">Live</span></span>
          </div>
        </div>

        <div class="section">
          <div class="section-title">Services</div>
          <div class="cards" id="svc-grid"></div>
        </div>

        <div class="section">
          <div class="section-title">Database</div>
          <div class="cards" id="db-grid"></div>
        </div>

        <div class="two-col">
          <div class="section">
            <div class="section-title">Recent Audit Log</div>
            <table>
              <thead><tr><th>Action</th><th>Time</th></tr></thead>
              <tbody id="audit-body"></tbody>
            </table>
          </div>
          <div class="section">
            <div class="section-title">Recent Events</div>
            <table>
              <thead><tr><th>Type</th><th>Project</th><th>Time</th></tr></thead>
              <tbody id="evt-body"></tbody>
            </table>
          </div>
        </div>

        <footer>Agent Paw Status Monitor &middot; localhost:47892 &middot; <span id="last-upd">—</span></footer>

        <script>
          function ago(iso) {
            const sec = Math.round((Date.now() - new Date(iso)) / 1000);
            if (sec < 60)    return sec + 's ago';
            if (sec < 3600)  return Math.floor(sec / 60) + 'm ago';
            if (sec < 86400) return Math.floor(sec / 3600) + 'h ago';
            return new Date(iso).toLocaleDateString();
          }

          function svcCard(label, running, sub) {
            const dot = running ? 'dot-on' : 'dot-off';
            const cls = running ? 'on' : 'off';
            const txt = running ? 'Running' : 'Stopped';
            return `<div class="card">
              <div class="card-label">${label}</div>
              <div class="svc-row">
                <div class="dot ${dot}"></div>
                <span class="svc-txt ${cls}">${txt}</span>
              </div>
              ${sub ? `<div class="sub-txt">${sub}</div>` : ''}
            </div>`;
          }

          function statCard(label, value, cls) {
            return `<div class="card db-card ${cls||''}">
              <div class="card-label">${label}</div>
              <div class="card-val">${value}</div>
            </div>`;
          }

          function actionTag(action) {
            const map = { LOGIN:'tag-login', LOGOUT:'tag-logout', DEV_BYPASS_LOGIN:'tag-dev', SESSION_FORCE_REVOKE:'tag-revoke' };
            return `<span class="tag ${map[action]||'tag-default'}">${action}</span>`;
          }

          async function refresh() {
            try {
              const d = await fetch('/api/status').then(r => r.json());

              document.getElementById('ver').textContent = 'v' + d.version;
              const bb = document.getElementById('build-badge');
              if (d.build === 'Debug') { bb.className = 'badge badge-debug'; bb.textContent = 'Debug'; }
              else { bb.className = 'badge badge-rel'; bb.textContent = 'Release'; }
              document.getElementById('uptime').textContent = d.uptimeLabel;
              document.getElementById('live-dot').className = 'live-dot';
              document.getElementById('live-txt').textContent = 'Live';

              const s = d.services;
              document.getElementById('svc-grid').innerHTML =
                svcCard('WebSocket',      s.websocket.running, s.websocket.running ? ':' + s.websocket.port : '') +
                svcCard('Google Pub/Sub', s.pubsub.running,    '') +
                svcCard('Slack',          s.slack.running,     '') +
                svcCard('Telegram',       s.telegram.running,  '');

              const db = d.database;
              document.getElementById('db-grid').innerHTML =
                statCard('PostgreSQL',      db.connected ? 'Connected' : 'Disconnected', db.connected ? 'ok' : 'err') +
                statCard('Active Users',    db.users,          '') +
                statCard('Projects',        db.projects,       '') +
                statCard('Active Sessions', db.activeSessions, '');

              const ab = document.getElementById('audit-body');
              ab.innerHTML = d.recentAudit.length
                ? d.recentAudit.map(a =>
                    `<tr><td>${actionTag(a.action)}</td><td class="t-ago">${ago(a.createdAt)}</td></tr>`
                  ).join('')
                : '<tr><td colspan="2" class="empty">No audit logs yet</td></tr>';

              const eb = document.getElementById('evt-body');
              eb.innerHTML = d.recentEvents.length
                ? d.recentEvents.map(e =>
                    `<tr>
                      <td class="evt-type">${e.eventType}</td>
                      <td class="t-dim">${e.projectId ? e.projectId.slice(0,8)+'…' : '—'}</td>
                      <td class="t-ago">${ago(e.createdAt)}</td>
                    </tr>`
                  ).join('')
                : '<tr><td colspan="3" class="empty">No events yet</td></tr>';

              document.getElementById('last-upd').textContent = 'Updated ' + new Date().toLocaleTimeString();
            } catch (err) {
              document.getElementById('live-dot').className = 'live-dot err';
              document.getElementById('live-txt').textContent = 'Disconnected';
              document.getElementById('last-upd').textContent = err.message;
            }
          }

          refresh();
          setInterval(refresh, 3000);
        </script>
        </body>
        </html>
        """;
}
