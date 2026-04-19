using AgentPaw.Orchestrator;

namespace AgentPaw.Services;

public class ChatDispatcherService
{
    private readonly ChatCommandService _commandService;
    private readonly GoogleChatService _chatService;
    private readonly OrchestratorService _orchestrator;
    private readonly AuthService _authService;

    public ChatDispatcherService(
        ChatCommandService commandService,
        GoogleChatService chatService,
        OrchestratorService orchestrator,
        AuthService authService)
    {
        _commandService = commandService;
        _chatService = chatService;
        _orchestrator = orchestrator;
        _authService = authService;
    }

    /// <summary>
    /// Google Chat Pub/Sub 전용 진입점 (기존 호환 유지)
    /// </summary>
    public async Task HandleChatEventAsync(ChatEvent evt)
    {
        if (evt.Type != "MESSAGE") return;
        if (evt.Message?.Sender?.Type == "BOT") return;

        var spaceName = evt.Space?.Name;
        if (string.IsNullOrEmpty(spaceName)) return;

        var text = GoogleChatService.StripBotMention(evt.Message?.Text ?? string.Empty);
        await HandleIncomingMessageAsync(spaceName, text, _chatService);
    }

    /// <summary>
    /// 플랫폼 공통 메시지 처리. Google Chat, Slack 모두 이 메서드를 사용.
    /// </summary>
    public async Task HandleIncomingMessageAsync(string channelOrSpace, string text, IChatPlatformSender sender)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var link = await _commandService.FindLinkBySpaceNameAsync(channelOrSpace);
        if (link == null || !link.Enabled) return;

        try
        {
            var result = await _commandService.ExecuteCommandAsync(link.LinkId, text);

            if (result.IsCommand)
            {
                if (result.Reply != null)
                    await sender.SendMessageAsync(channelOrSpace, result.Reply);
                return;
            }

            var session = await _commandService.GetOrCreateSessionAsync(link.LinkId);
            if (session.ActiveProjectId == null)
            {
                await sender.SendMessageAsync(channelOrSpace,
                    "프로젝트가 설정되지 않았습니다.\n`/projects` 로 프로젝트 목록을 확인하고 `/project <이름>` 으로 설정하세요.");
                return;
            }

            // 명시적 위키 조회 의도("위키" 키워드 + 조회성 동사)는 오케스트레이터 호출 없이 즉시 위키 검색으로 응답한다.
            var wikiIntent = WikiIntentDetector.TryDetect(result.Message ?? text);
            if (wikiIntent != null)
            {
                var wikiReply = await _commandService.ExecuteCommandAsync(
                    link.LinkId,
                    string.IsNullOrWhiteSpace(wikiIntent.Keyword) ? "/wiki" : $"/wiki {wikiIntent.Keyword}");
                if (wikiReply.Reply != null)
                {
                    await sender.SendMessageAsync(channelOrSpace, wikiReply.Reply);
                    return;
                }
            }

            var userId = _authService.CurrentUserId;
            if (userId == null)
            {
                await sender.SendMessageAsync(channelOrSpace, "Agent Paw에 로그인이 필요합니다.");
                return;
            }

            var input = new OrchestratorInput
            {
                ProjectId = session.ActiveProjectId,
                UserId = userId,
                Message = result.Message ?? text,
                ForcePersonaId = session.ActivePersonaId
            };

            var output = await _orchestrator.RunPipelineAsync(input);

            if (output.NeedsConfirmation)
            {
                await sender.SendMessageAsync(channelOrSpace, output.Content);
            }
            else
            {
                foreach (var turn in output.Turns)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"*[{turn.PersonaLabel}]*\n\n");
                    if (!string.IsNullOrWhiteSpace(turn.Content))
                        sb.Append(turn.Content);

                    if (turn.ToolCalls.Count > 0)
                    {
                        if (sb.Length > 0) sb.Append("\n\n");
                        foreach (var t in turn.ToolCalls)
                        {
                            var mark = t.Success ? "✓" : "✗";
                            sb.Append($"🔧 {mark} {t.Name}({t.ArgsSummary}) → {t.Result}\n");
                        }
                    }

                    if (turn.HandoffToLabel != null)
                        sb.Append($"\n_↳ {turn.HandoffToLabel}에게 요청: {turn.HandoffRequest}_");

                    await sender.SendMessageAsync(channelOrSpace, sb.ToString().TrimEnd());
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
                await sender.SendMessageAsync(channelOrSpace, $"⚠ 오류가 발생했습니다: {ex.Message}");
            }
            catch { }
        }
    }
}

// Google Chat Pub/Sub 이벤트 모델
public class ChatEvent
{
    public string Type { get; set; } = string.Empty;
    public ChatEventMessage? Message { get; set; }
    public ChatEventSpace? Space { get; set; }
}

public class ChatEventMessage
{
    public string? Text { get; set; }
    public ChatEventSender? Sender { get; set; }
}

public class ChatEventSender
{
    public string? Type { get; set; } // HUMAN, BOT
}

public class ChatEventSpace
{
    public string? Name { get; set; }
}
