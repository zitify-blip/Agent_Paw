using AgentPaw.Models;

namespace AgentPaw.Orchestrator;

public class ClassifierService
{
    public ClassificationResult Classify(string userMessage, List<Persona> personas, string? forcePersonaId)
    {
        // 강제 지정된 페르소나 (디버깅·테스트용 PM 우회)
        if (!string.IsNullOrEmpty(forcePersonaId))
        {
            var forced = personas.FirstOrDefault(p => p.PersonaId == forcePersonaId);
            if (forced != null)
                return new ClassificationResult { PersonaId = forced.PersonaId, Confidence = 1.0, NeedsConfirmation = false };
        }

        // PM 허브 라우팅 — is_pm=true 페르소나가 존재하면 항상 PM으로 우선 라우팅한다
        var pm = personas.FirstOrDefault(p => p.IsPm);
        if (pm != null)
            return new ClassificationResult { PersonaId = pm.PersonaId, Confidence = 1.0, NeedsConfirmation = false };

        // 페르소나가 1개면 자동 선택
        if (personas.Count == 1)
            return new ClassificationResult { PersonaId = personas[0].PersonaId, Confidence = 1.0, NeedsConfirmation = false };

        // 키워드 매칭
        var messageLower = userMessage.ToLowerInvariant();
        var bestMatch = (Persona?)null;
        var bestScore = 0;

        foreach (var persona in personas)
        {
            if (string.IsNullOrWhiteSpace(persona.Keywords)) continue;

            var keywords = persona.Keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var score = keywords.Count(k => messageLower.Contains(k.ToLowerInvariant()));

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = persona;
            }
        }

        if (bestMatch != null && bestScore > 0)
            return new ClassificationResult { PersonaId = bestMatch.PersonaId, Confidence = 0.85, NeedsConfirmation = false };

        // 매칭 없음 — 랜덤 페르소나 선택
        var picked = personas.Count > 0
            ? personas[Random.Shared.Next(personas.Count)]
            : null;
        return new ClassificationResult
        {
            PersonaId = picked?.PersonaId ?? string.Empty,
            Confidence = 0.5,
            NeedsConfirmation = false
        };
    }
}

public class ClassificationResult
{
    public string PersonaId { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public bool NeedsConfirmation { get; set; }
}
