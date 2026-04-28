using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentPaw.Services;

public class AiClientService
{
    private readonly ApiKeyService _apiKeyService;
    private readonly ClaudeCliService _claudeCliService;
    private readonly HttpClient _httpClient;

    private static readonly Dictionary<string, string> ClaudeModelMap = new()
    {
        ["claude-sonnet"] = "claude-sonnet-4-6-20250627",
        ["claude-opus"] = "claude-opus-4-6-20250627",
        ["claude-haiku"] = "claude-haiku-4-5-20251001",
        // 폐기 모델 호환 매핑
        ["claude-sonnet-4-20250514"] = "claude-sonnet-4-6-20250627",
        ["claude-opus-4-20250514"] = "claude-opus-4-6-20250627"
    };

    private static readonly Dictionary<string, string> GeminiModelMap = new()
    {
        ["gemini-pro"] = "gemini-2.5-pro",
        ["gemini-flash"] = "gemini-2.5-flash",
        ["gemini-flash-lite"] = "gemini-2.0-flash-lite",
        // 폐기 모델 호환 매핑
        ["gemini-1.5-pro"] = "gemini-2.5-pro",
        ["gemini-2.0-flash"] = "gemini-2.5-flash"
    };

    public AiClientService(ApiKeyService apiKeyService, ClaudeCliService claudeCliService, IHttpClientFactory httpClientFactory)
    {
        _apiKeyService = apiKeyService;
        _claudeCliService = claudeCliService;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<string> ChatWithFastModelAsync(
        string systemPrompt,
        string userMessage,
        int maxTokens = 2048,
        CancellationToken ct = default)
    {
        var response = await ChatWithModelStreamAsync(
            "claude-haiku", systemPrompt, userMessage,
            temperature: 0.3f, maxTokens, onDelta: null, history: null, ct)
            .ConfigureAwait(false);
        return response.Content;
    }

    public Task<AiResponse> ChatWithFallbackAsync(
        string primaryModel,
        string? fallbackModel,
        string systemPrompt,
        string userMessage,
        float temperature = 0.7f,
        int maxTokens = 4096)
        => ChatWithFallbackStreamAsync(primaryModel, fallbackModel, systemPrompt, userMessage,
            temperature, maxTokens, onDelta: null);

    public async Task<AiResponse> ChatWithFallbackStreamAsync(
        string primaryModel,
        string? fallbackModel,
        string systemPrompt,
        string userMessage,
        float temperature,
        int maxTokens,
        Action<string>? onDelta,
        IReadOnlyList<ConversationTurn>? history = null,
        CancellationToken ct = default)
    {
        var models = new List<string> { primaryModel };
        if (!string.IsNullOrEmpty(fallbackModel))
            models.Add(fallbackModel);

        var skipped = new List<string>();
        var errors = new List<string>();

        foreach (var model in models)
        {
            var provider = ApiKeyService.ModelToProvider(model);

            // Claude CLI가 활성화되어 있으면 API Key 없이도 시도 가능
            var cliAvailable = provider == "CLAUDE" && await _claudeCliService.IsEnabledAsync().ConfigureAwait(false);
            var hasKey = await _apiKeyService.HasApiKeyAsync(provider).ConfigureAwait(false);

            if (!hasKey && !cliAvailable)
            {
                skipped.Add($"{model}({provider} API 키 미등록)");
                continue;
            }

            try
            {
                return await ChatWithModelStreamAsync(model, systemPrompt, userMessage, temperature, maxTokens, onDelta, history, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                errors.Add($"{model}: {ex.Message}");
            }
        }

        var detail = string.Join(" | ", new[]
        {
            skipped.Count > 0 ? $"건너뜀: {string.Join(", ", skipped)}" : null,
            errors.Count > 0 ? $"실패: {string.Join("; ", errors)}" : null
        }.Where(s => s != null));

        throw new InvalidOperationException(
            $"ALL_MODELS_FAILED: {(string.IsNullOrEmpty(detail) ? "사용 가능한 모델이 없습니다." : detail)} — 설정에서 API 키를 확인하세요.");
    }

    public Task<AiResponse> ChatWithModelAsync(
        string model,
        string systemPrompt,
        string userMessage,
        float temperature = 0.7f,
        int maxTokens = 4096)
        => ChatWithModelStreamAsync(model, systemPrompt, userMessage, temperature, maxTokens, onDelta: null);

    public async Task<AiResponse> ChatWithModelStreamAsync(
        string model,
        string systemPrompt,
        string userMessage,
        float temperature,
        int maxTokens,
        Action<string>? onDelta,
        IReadOnlyList<ConversationTurn>? history = null,
        CancellationToken ct = default)
    {
        var provider = ApiKeyService.ModelToProvider(model);

        if (provider == "CLAUDE")
        {
            // Claude CLI 우선 시도 → 실패 시 API Key fallback
            // CLI는 스트리밍 미지원 → 전체 응답을 한 번에 onDelta로 전달한다
            if (await _claudeCliService.IsEnabledAsync().ConfigureAwait(false))
            {
                try
                {
                    var cliMsg = FormatHistoryAsText(userMessage, history);
                    var content = await _claudeCliService.CallAsync(systemPrompt, cliMsg).ConfigureAwait(false);
                    onDelta?.Invoke(content);
                    return new AiResponse
                    {
                        Content = content,
                        ModelUsed = $"{model} (CLI)",
                        Provider = "CLAUDE_CLI"
                    };
                }
                catch
                {
                    // CLI 실패 → API Key가 있으면 fallback
                    if (await _apiKeyService.HasApiKeyAsync("CLAUDE").ConfigureAwait(false))
                        return await CallClaudeStreamAsync(model, systemPrompt, userMessage, temperature, maxTokens, onDelta, history, ct).ConfigureAwait(false);

                    throw new InvalidOperationException("CLAUDE_CLI_FAILED: Claude Code CLI 호출 실패. API 키도 설정되지 않았습니다.");
                }
            }

            return await CallClaudeStreamAsync(model, systemPrompt, userMessage, temperature, maxTokens, onDelta, history, ct).ConfigureAwait(false);
        }

        if (provider == "GEMINI")
            return await CallGeminiStreamAsync(model, systemPrompt, userMessage, temperature, maxTokens, onDelta, history, ct).ConfigureAwait(false);

        throw new InvalidOperationException($"Unknown provider: {provider}");
    }

    private async Task<AiResponse> CallClaudeStreamAsync(
        string model, string systemPrompt, string userMessage,
        float temperature, int maxTokens, Action<string>? onDelta,
        IReadOnlyList<ConversationTurn>? history, CancellationToken ct)
    {
        var apiKey = await _apiKeyService.GetApiKeyAsync("CLAUDE").ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Claude API 키가 설정되지 않았습니다.");

        var resolvedModel = ClaudeModelMap.GetValueOrDefault(model, model);

        var messages = new List<object>();
        if (history != null)
            foreach (var t in history)
                messages.Add(new { role = t.Role, content = t.Content });
        messages.Add(new { role = "user", content = userMessage });

        var request = new
        {
            model = resolvedModel,
            max_tokens = maxTokens,
            temperature,
            system = new[]
            {
                new { type = "text", text = systemPrompt, cache_control = new { type = "ephemeral" } }
            },
            stream = true,
            messages
        };

        var json = JsonSerializer.Serialize(request);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Headers.Add("anthropic-beta", "prompt-caching-2024-07-31");

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Claude API error ({response.StatusCode}): {err}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var sb = new StringBuilder();
        int inputTokens = 0;
        int outputTokens = 0;

        while (true)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line == null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var data = line[5..].TrimStart();
            if (string.IsNullOrEmpty(data) || data == "[DONE]") continue;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeEl)) continue;
                var type = typeEl.GetString();

                if (type == "content_block_delta"
                    && root.TryGetProperty("delta", out var delta)
                    && delta.TryGetProperty("type", out var dType)
                    && dType.GetString() == "text_delta"
                    && delta.TryGetProperty("text", out var txtEl))
                {
                    var chunk = txtEl.GetString() ?? string.Empty;
                    if (chunk.Length > 0)
                    {
                        sb.Append(chunk);
                        onDelta?.Invoke(chunk);
                    }
                }
                else if (type == "message_start"
                    && root.TryGetProperty("message", out var msg)
                    && msg.TryGetProperty("usage", out var usage0)
                    && usage0.TryGetProperty("input_tokens", out var it))
                {
                    inputTokens = it.GetInt32();
                }
                else if (type == "message_delta"
                    && root.TryGetProperty("usage", out var usage1)
                    && usage1.TryGetProperty("output_tokens", out var ot))
                {
                    outputTokens = ot.GetInt32();
                }
            }
            catch (JsonException)
            {
                // malformed SSE line — skip
            }
        }

        return new AiResponse
        {
            Content = sb.ToString(),
            ModelUsed = resolvedModel,
            Provider = "CLAUDE",
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };
    }

    private async Task<AiResponse> CallGeminiStreamAsync(
        string model, string systemPrompt, string userMessage,
        float temperature, int maxTokens, Action<string>? onDelta,
        IReadOnlyList<ConversationTurn>? history, CancellationToken ct)
    {
        var apiKey = await _apiKeyService.GetApiKeyAsync("GEMINI").ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Gemini API 키가 설정되지 않았습니다.");

        var resolvedModel = GeminiModelMap.GetValueOrDefault(model, model);

        // Gemini: system prompt를 user message 앞에 병합 (멀티바이트 protobuf 이슈 회피)
        var combinedMessage = $"{systemPrompt}\n\n---\n\n{FormatHistoryAsText(userMessage, history)}";

        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = combinedMessage }
                    }
                }
            },
            generationConfig = new
            {
                temperature,
                maxOutputTokens = maxTokens
            }
        };

        var json = JsonSerializer.Serialize(request);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{resolvedModel}:streamGenerateContent?alt=sse&key={apiKey}";

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Gemini API error ({response.StatusCode}): {err}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var sb = new StringBuilder();
        int inputTokens = 0;
        int outputTokens = 0;

        while (true)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line == null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var data = line[5..].TrimStart();
            if (string.IsNullOrEmpty(data)) continue;

            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var cands) && cands.ValueKind == JsonValueKind.Array && cands.GetArrayLength() > 0)
                {
                    var first = cands[0];
                    if (first.TryGetProperty("content", out var content)
                        && content.TryGetProperty("parts", out var parts)
                        && parts.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var p in parts.EnumerateArray())
                        {
                            if (p.TryGetProperty("text", out var txtEl))
                            {
                                var chunk = txtEl.GetString() ?? string.Empty;
                                if (chunk.Length > 0)
                                {
                                    sb.Append(chunk);
                                    onDelta?.Invoke(chunk);
                                }
                            }
                        }
                    }
                }

                if (root.TryGetProperty("usageMetadata", out var usage))
                {
                    if (usage.TryGetProperty("promptTokenCount", out var it))
                        inputTokens = it.GetInt32();
                    if (usage.TryGetProperty("candidatesTokenCount", out var ot))
                        outputTokens = ot.GetInt32();
                }
            }
            catch (JsonException)
            {
                // malformed SSE line — skip
            }
        }

        return new AiResponse
        {
            Content = sb.ToString(),
            ModelUsed = resolvedModel,
            Provider = "GEMINI",
            InputTokens = inputTokens,
            OutputTokens = outputTokens
        };
    }

    // 히스토리가 없으면 원본 메시지, 있으면 이전 대화를 텍스트로 prefix해서 반환
    private static string FormatHistoryAsText(string userMessage, IReadOnlyList<ConversationTurn>? history)
    {
        if (history == null || history.Count == 0) return userMessage;
        var sb = new StringBuilder();
        sb.AppendLine("[이전 대화 기록]");
        foreach (var t in history)
        {
            var label = t.Role == "user" ? "User" : "Assistant";
            sb.AppendLine($"\n[{label}]");
            sb.AppendLine(t.Content);
        }
        sb.AppendLine("\n---\n[현재 요청]");
        sb.Append(userMessage);
        return sb.ToString();
    }
}

// --- Conversation history DTO ---

public class ConversationTurn
{
    public string Role { get; set; } = string.Empty; // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
}

// --- Response DTOs ---

public class AiResponse
{
    public string Content { get; set; } = string.Empty;
    public string ModelUsed { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
}

// Claude API response
internal class ClaudeResponse
{
    [JsonPropertyName("content")]
    public List<ClaudeContentBlock>? Content { get; set; }

    [JsonPropertyName("usage")]
    public ClaudeUsage? Usage { get; set; }
}

internal class ClaudeContentBlock
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

internal class ClaudeUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

// Gemini API response
internal class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }

    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

internal class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }
}

internal class GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart>? Parts { get; set; }
}

internal class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

internal class GeminiUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }
}
