using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AgentPaw.Data;

namespace AgentPaw.Services;

public class WebSocketServerService
{
    private readonly IDbContextFactory<AgentPawDbContext> _dbFactory;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, WebSocketClient> _clients = new();

    private const int Port = 8765;

    public bool IsRunning { get; private set; }

    public WebSocketServerService(IDbContextFactory<AgentPawDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{Port}/");

        try
        {
            _listener.Start();
            IsRunning = true;
            _ = AcceptConnectionsAsync(_cts.Token);
        }
        catch
        {
            IsRunning = false;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();

        foreach (var client in _clients.Values)
        {
            try { client.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "서버 종료", CancellationToken.None).Wait(1000); }
            catch { }
        }
        _clients.Clear();

        try { _listener?.Stop(); }
        catch { }

        _listener = null;
        _cts?.Dispose();
        _cts = null;
        IsRunning = false;
    }

    private async Task AcceptConnectionsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener?.IsListening == true)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                if (!context.Request.IsWebSocketRequest)
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                    continue;
                }

                _ = HandleConnectionAsync(context, ct);
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) { break; }
            catch { }
        }
    }

    private async Task HandleConnectionAsync(HttpListenerContext context, CancellationToken ct)
    {
        WebSocket? ws = null;
        try
        {
            // 토큰 인증
            var query = context.Request.QueryString;
            var token = query["token"];
            if (string.IsNullOrEmpty(token))
            {
                context.Response.StatusCode = 401;
                context.Response.Close();
                return;
            }

            var userId = await AuthenticateTokenAsync(token);
            if (userId == null)
            {
                context.Response.StatusCode = 403;
                context.Response.Close();
                return;
            }

            var wsContext = await context.AcceptWebSocketAsync(null);
            ws = wsContext.WebSocket;

            var clientId = Guid.NewGuid().ToString();
            var client = new WebSocketClient { Socket = ws, UserId = userId };
            _clients[clientId] = client;

            // 메시지 수신 루프
            var buffer = new byte[4096];
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
            }

            _clients.TryRemove(clientId, out _);
        }
        catch
        {
            // 연결 종료
        }
        finally
        {
            if (ws?.State == WebSocketState.Open)
            {
                try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); }
                catch { }
            }
        }
    }

    private async Task<string?> AuthenticateTokenAsync(string token)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var authToken = await db.AuthTokens
            .FirstOrDefaultAsync(t => t.TokenValue == token && t.TokenType == "GOOGLE_SPACE_REFRESH" && !t.IsRevoked);
        return authToken?.UserId;
    }

    public async Task BroadcastNotificationAsync(SpaceNotification notification)
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "notification",
            notification.Event,
            notification.ProjectId,
            notification.ProjectName,
            notification.Message
        });
        var bytes = Encoding.UTF8.GetBytes(json);

        foreach (var client in _clients.Values.ToList())
        {
            if (client.Socket.State != WebSocketState.Open) continue;

            try
            {
                await client.Socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch { }
        }
    }

    // 편의 알림 메서드
    public Task NotifyBuildSuccessAsync(string projectName, string commitHash, string duration) =>
        BroadcastNotificationAsync(new SpaceNotification
        {
            Event = "BUILD_SUCCESS",
            ProjectName = projectName,
            Message = $"✅ [{projectName}] Build Success — {commitHash[..8]} {duration}"
        });

    public Task NotifyBuildFailureAsync(string projectName, string error) =>
        BroadcastNotificationAsync(new SpaceNotification
        {
            Event = "BUILD_FAILURE",
            ProjectName = projectName,
            Message = $"❌ [{projectName}] Build Failure — {error}"
        });

    public Task NotifyAiQuestionAsync(string projectName, string engine, string question) =>
        BroadcastNotificationAsync(new SpaceNotification
        {
            Event = "AI_QUESTION",
            ProjectName = projectName,
            Message = $"❓ [{projectName}] {engine} asks: {question}"
        });

    public Task NotifyMilestoneAsync(string projectName, string milestone) =>
        BroadcastNotificationAsync(new SpaceNotification
        {
            Event = "MILESTONE",
            ProjectName = projectName,
            Message = $"🎯 [{projectName}] Milestone — {milestone}"
        });

    public Task NotifyEmergencyAsync(string projectName, string type, string detail) =>
        BroadcastNotificationAsync(new SpaceNotification
        {
            Event = "EMERGENCY",
            ProjectName = projectName,
            Message = $"🚨 [{projectName}] Emergency — {type}: {detail}"
        });
}

public class WebSocketClient
{
    public WebSocket Socket { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
}

public class SpaceNotification
{
    public string Event { get; set; } = string.Empty;
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string Message { get; set; } = string.Empty;
}
