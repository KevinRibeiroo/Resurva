using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Infrastructure;

public sealed class MockResumeOptimizationProvider : IResumeOptimizationProvider
{
    public string ModelName => "Mock-Optimization-Engine";
    public string ConfigurationFingerprint => "mock-v1";
    public string PromptVersion => "v1-opt";

    public Task<IReadOnlyList<OptimizationSuggestionModel>> GenerateSuggestionsAsync(
        string resumeText,
        string jobDescription,
        StructuredComparisonModel comparisonContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var suggestions = new List<OptimizationSuggestionModel>();

        // 1. Safe Suggestion: Refine / rephrase existing supported evidence
        var firstMatched = comparisonContext.MatchedSkills.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Evidence))
            ?? comparisonContext.RequirementsMet.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Evidence));

        if (firstMatched != null && !string.IsNullOrWhiteSpace(firstMatched.Evidence) && resumeText.Contains(firstMatched.Evidence, StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add(new OptimizationSuggestionModel(
                Id: Guid.NewGuid(),
                Level: OptimizationSafetyLevel.Safe,
                Confirmed: false,
                OriginalText: firstMatched.Evidence,
                ProposedText: $"{firstMatched.Evidence} (com foco em entregas de alto impacto e boas práticas)",
                Reason: "Destaque e clareza da competência já comprovada no histórico profissional em relação à vaga.",
                Evidence: firstMatched.Evidence));
        }
        else
        {
            // Fallback safe suggestion using a common term if available or general formatting
            var lines = resumeText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var targetLine = lines.FirstOrDefault(l => l.Length is >= 10 and <= 80);
            if (targetLine != null)
            {
                suggestions.Add(new OptimizationSuggestionModel(
                    Id: Guid.NewGuid(),
                    Level: OptimizationSafetyLevel.Safe,
                    Confirmed: false,
                    OriginalText: targetLine,
                    ProposedText: $"{targetLine} — atuação voltada a resultados e qualidade técnica",
                    Reason: "Aprimoramento de redação profissional preservando integralmente os fatos descritos.",
                    Evidence: targetLine));
            }
        }

        // 2. NeedsConfirmation Suggestion: Skill or requirement missing from resume
        var firstMissing = comparisonContext.MissingSkills.FirstOrDefault()
            ?? comparisonContext.RequirementsMissing.FirstOrDefault();

        var missingName = firstMissing?.Text ?? "PostgreSQL";
        suggestions.Add(new OptimizationSuggestionModel(
            Id: Guid.NewGuid(),
            Level: OptimizationSafetyLevel.NeedsConfirmation,
            Confirmed: false,
            OriginalText: "",
            ProposedText: $"Conhecimento prático e aplicação de {missingName} em projetos de software.",
            Reason: $"A descrição da vaga enfatiza {missingName}, que não estava explicitamente citado no currículo.",
            Evidence: null,
            ConfirmationQuestion: $"Você possui conhecimento ou experiência com {missingName}?"));

        // 3. Forbidden Suggestion: Fabrication of credentials or managerial scope
        suggestions.Add(new OptimizationSuggestionModel(
            Id: Guid.NewGuid(),
            Level: OptimizationSafetyLevel.Forbidden,
            Confirmed: false,
            OriginalText: "",
            ProposedText: "Liderança de equipe técnica de 15 engenheiros e gestão orçamentária anual de R$ 3 milhões.",
            Reason: "Alegação de escopo de liderança executiva sem qualquer base factual no documento original.",
            Evidence: null,
            ConfirmationQuestion: null));

        return Task.FromResult<IReadOnlyList<OptimizationSuggestionModel>>(suggestions);
    }
}
