using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AgentPaw.Services;

public class GoogleDocsService
{
    private readonly HttpClient _http;

    public GoogleDocsService(IHttpClientFactory httpClientFactory)
    {
        _http = httpClientFactory.CreateClient();
    }

    // Google Docs URL 또는 순수 ID에서 docId 추출
    public static string? ExtractDocId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        input = input.Trim();

        // https://docs.google.com/document/d/{id}/...
        const string marker = "/document/d/";
        var idx = input.IndexOf(marker, StringComparison.Ordinal);
        if (idx >= 0)
        {
            var start = idx + marker.Length;
            var end = input.IndexOf('/', start);
            return end >= 0 ? input[start..end] : input[start..];
        }

        // 그 외는 그대로 ID로 간주
        return input;
    }

    /// <summary>
    /// 지정된 Google 문서에 <paramref name="content"/>를 덮어씁니다.
    /// </summary>
    public async Task<(bool Success, string Error)> ExportAsync(string docId, string accessToken, string content)
    {
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // 1. 현재 문서 길이 조회
        var getResp = await _http.GetAsync(
            $"https://docs.googleapis.com/v1/documents/{docId}?fields=body.content(endIndex)");

        if (!getResp.IsSuccessStatusCode)
        {
            var errBody = await getResp.Content.ReadAsStringAsync();
            if (getResp.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                getResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "Google Docs 권한이 없습니다. 로그아웃 후 다시 로그인하세요.");
            return (false, $"문서 조회 실패 ({getResp.StatusCode}): {errBody}");
        }

        int endIndex = 1;
        try
        {
            using var docJson = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
            if (docJson.RootElement.TryGetProperty("body", out var body) &&
                body.TryGetProperty("content", out var bodyContent) &&
                bodyContent.ValueKind == JsonValueKind.Array)
            {
                var arr = bodyContent.EnumerateArray().ToList();
                if (arr.Count > 0 && arr[^1].TryGetProperty("endIndex", out var ei))
                    endIndex = ei.GetInt32();
            }
        }
        catch { }

        // 2. batchUpdate: 기존 내용 삭제 → 새 내용 삽입
        var requests = new List<object>();
        if (endIndex > 2)
        {
            requests.Add(new
            {
                deleteContentRange = new
                {
                    range = new { startIndex = 1, endIndex = endIndex - 1 }
                }
            });
        }
        requests.Add(new
        {
            insertText = new
            {
                location = new { index = 1 },
                text = content
            }
        });

        var payload = JsonSerializer.Serialize(new { requests });
        var patchResp = await _http.PostAsync(
            $"https://docs.googleapis.com/v1/documents/{docId}:batchUpdate",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        if (!patchResp.IsSuccessStatusCode)
        {
            var errBody = await patchResp.Content.ReadAsStringAsync();
            return (false, $"내보내기 실패 ({patchResp.StatusCode}): {errBody}");
        }

        return (true, string.Empty);
    }
}
