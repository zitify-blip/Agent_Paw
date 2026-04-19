namespace AgentPaw.Orchestrator;

public class SelfCriticService
{
    public CriticResult Evaluate(string aiResponse, string userMessage)
    {
        // 현재 스텁: 항상 통과. Phase 후반에 AI 기반 검증 구현 예정.
        return new CriticResult { Passed = true, Reason = string.Empty, Suggestions = [] };
    }
}

public class CriticResult
{
    public bool Passed { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> Suggestions { get; set; } = [];
}
