using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public sealed class ResumeOptimizationService(
    IResumeRepository resumes,
    IAnalysisRepository analyses,
    IResumeOptimizationRepository optimizations,
    IResumeOptimizationProvider optimizationProvider,
    IOptimizationSafetyValidator safetyValidator,
    ICurrentUser currentUser,
    TimeProvider clock,
    ILogger<ResumeOptimizationService>? logger = null) : IResumeOptimizationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
    private readonly ILogger<ResumeOptimizationService> _logger = logger ?? NullLogger<ResumeOptimizationService>.Instance;

    public async Task<OptimizationPlanModel> CreatePlanAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var analysis = await analyses.GetAsync(analysisId, cancellationToken)
            ?? throw new ResourceNotFoundException("Analysis not found.");

        var resume = await resumes.GetAsync(analysis.ResumeId, cancellationToken)
            ?? throw new ResourceNotFoundException("Resume not found.");

        AnalysisResultModel? analysisResult = null;
        try
        {
            analysisResult = JsonSerializer.Deserialize<AnalysisResultModel>(analysis.ResultJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize AnalysisResultModel for AnalysisId {AnalysisId}", analysisId);
        }

        var comparisonContext = analysisResult is not null
            ? new StructuredComparisonModel(
                analysisResult.MatchedSkills,
                analysisResult.MissingSkills,
                analysisResult.RequirementsMet,
                analysisResult.RequirementsMissing,
                analysisResult.Strengths,
                analysisResult.PointsOfAttention,
                analysisResult.Recommendations,
                analysisResult.ExperienceScore,
                analysisResult.SeniorityScore,
                analysisResult.EducationScore)
            : new StructuredComparisonModel([], [], [], [], [], [], [], 0, 0, 0);

        _logger.LogInformation(
            "Generating optimization suggestions using provider {Provider} for AnalysisId {AnalysisId} (ResumeId: {ResumeId})",
            optimizationProvider.GetType().Name, analysis.Id, resume.Id);

        var rawSuggestions = await optimizationProvider.GenerateSuggestionsAsync(
            resume.ExtractedText,
            analysis.JobDescription,
            comparisonContext,
            cancellationToken);

        var validatedSuggestions = new List<OptimizationSuggestionModel>();
        foreach (var raw in rawSuggestions)
        {
            var id = raw.Id == Guid.Empty ? Guid.NewGuid() : raw.Id;
            var level = raw.Level;
            var evidence = string.IsNullOrWhiteSpace(raw.Evidence) ? null : raw.Evidence.Trim();
            var originalText = raw.OriginalText?.Trim() ?? "";
            var proposedText = raw.ProposedText?.Trim() ?? "";
            var reason = raw.Reason?.Trim() ?? "";
            var confirmationQuestion = raw.ConfirmationQuestion?.Trim();

            // Safety Rule 1: A suggestion can never be Safe if it lacks supporting evidence in the original resume.
            if (level == OptimizationSafetyLevel.Safe && string.IsNullOrEmpty(evidence))
            {
                _logger.LogInformation(
                    "Downgrading suggestion '{ProposedText}' from Safe to NeedsConfirmation due to lack of textual evidence.",
                    proposedText);
                level = OptimizationSafetyLevel.NeedsConfirmation;
                if (string.IsNullOrEmpty(confirmationQuestion))
                {
                    confirmationQuestion = $"Você confirma a seguinte informação para inclusão no currículo: \"{proposedText}\"?";
                }
            }

            // Safety Rule 2: If an original text is specified for replacement, it MUST actually exist in the resume text.
            if (!string.IsNullOrEmpty(originalText) && !resume.ExtractedText.Contains(originalText, StringComparison.OrdinalIgnoreCase))
            {
                if (level == OptimizationSafetyLevel.Safe)
                {
                    level = OptimizationSafetyLevel.NeedsConfirmation;
                    if (string.IsNullOrEmpty(confirmationQuestion))
                    {
                        confirmationQuestion = $"O trecho original \"{originalText}\" não foi localizado com precisão. Você confirma a substituição proposta: \"{proposedText}\"?";
                    }
                }
            }

            // Safety Rule 3: If NeedsConfirmation, ensure there is a clear confirmation question.
            if (level == OptimizationSafetyLevel.NeedsConfirmation && string.IsNullOrEmpty(confirmationQuestion))
            {
                confirmationQuestion = $"Você confirma a seguinte afirmação sobre sua experiência ou habilidades: \"{proposedText}\"?";
            }

            validatedSuggestions.Add(new OptimizationSuggestionModel(
                Id: id,
                Level: level,
                Confirmed: false,
                OriginalText: originalText,
                ProposedText: proposedText,
                Reason: reason,
                Evidence: evidence,
                ConfirmationQuestion: confirmationQuestion));
        }

        var entity = new ResumeOptimizationEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = currentUser.UserId,
            ResumeId = resume.Id,
            AnalysisId = analysis.Id,
            OriginalText = resume.ExtractedText,
            Version = 1,
            Status = "Pending",
            SuggestionsJson = JsonSerializer.Serialize(validatedSuggestions, JsonOptions),
            CreatedAt = clock.GetUtcNow(),
            UpdatedAt = clock.GetUtcNow()
        };

        await optimizations.AddAsync(entity, cancellationToken);

        _logger.LogInformation(
            "Created OptimizationPlan with ID {OptimizationId} for AnalysisId {AnalysisId} ({Count} suggestions)",
            entity.Id, analysis.Id, validatedSuggestions.Count);

        return new OptimizationPlanModel(
            entity.Id,
            entity.AnalysisId,
            entity.ResumeId,
            entity.Version,
            entity.Status,
            entity.OriginalText,
            validatedSuggestions,
            entity.CreatedAt);
    }

    public async Task<OptimizationPlanModel?> GetPlanAsync(Guid optimizationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await optimizations.GetAsync(optimizationId, cancellationToken);
        if (entity is null)
            return null;

        var suggestions = JsonSerializer.Deserialize<List<OptimizationSuggestionModel>>(entity.SuggestionsJson, JsonOptions) ?? [];

        return new OptimizationPlanModel(
            entity.Id,
            entity.AnalysisId,
            entity.ResumeId,
            entity.Version,
            entity.Status,
            entity.OriginalText,
            suggestions,
            entity.CreatedAt,
            entity.AdaptedText);
    }

    public async Task<OptimizationResultModel> ApplyDecisionsAsync(
        Guid optimizationId,
        ApplyOptimizationCommand command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (command.Decisions is null)
            throw new ArgumentException("Decisions list is required.");

        var entity = await optimizations.GetAsync(optimizationId, cancellationToken)
            ?? throw new ResourceNotFoundException("Optimization plan not found.");

        // Idempotency and status check
        if (entity.Status == "Applied")
        {
            if (entity.DecisionsJson is not null)
            {
                var snapshot = JsonSerializer.Deserialize<AppliedOptimizationSnapshot>(entity.DecisionsJson, JsonOptions);
                if (snapshot is not null && AreDecisionsEquivalent(snapshot.Decisions, command.Decisions))
                {
                    _logger.LogInformation("Idempotent replay of applied decisions for OptimizationId {OptimizationId}", optimizationId);
                    return new OptimizationResultModel(
                        entity.Id,
                        entity.AnalysisId,
                        entity.ResumeId,
                        entity.OriginalText,
                        entity.AdaptedText ?? entity.OriginalText,
                        snapshot.AppliedItems,
                        entity.UpdatedAt);
                }
            }

            throw new OptimizationConflictException("The optimization plan has already been applied with different decisions.");
        }

        if (entity.Version != command.Version)
        {
            throw new OptimizationConflictException(
                FormattableString.Invariant($"The optimization plan version is outdated (Expected {entity.Version}, got {command.Version})."));
        }

        // Check for duplicate decisions
        if (command.Decisions.GroupBy(d => d.SuggestionId).Any(g => g.Count() > 1))
            throw new ArgumentException("Duplicate decisions received for the same suggestion.");

        var canonicalSuggestions = JsonSerializer.Deserialize<List<OptimizationSuggestionModel>>(entity.SuggestionsJson, JsonOptions) ?? [];
        var suggestionsById = canonicalSuggestions.ToDictionary(s => s.Id);

        var appliedItems = new List<AppliedOptimizationItemModel>();
        var rejectedOrIgnored = new List<Guid>();

        foreach (var decision in command.Decisions)
        {
            if (!suggestionsById.TryGetValue(decision.SuggestionId, out var canonical))
                throw new ArgumentException($"Suggestion with ID '{decision.SuggestionId}' does not exist in this optimization plan.");

            if (!decision.Accepted)
            {
                rejectedOrIgnored.Add(decision.SuggestionId);
                continue;
            }

            // Security Rule: Evaluate canonical suggestion with user's explicit confirmation flag
            var evaluated = canonical with { Confirmed = decision.Confirmed };
            if (!safetyValidator.CanApply(evaluated))
            {
                _logger.LogWarning(
                    "Suggestion '{SuggestionId}' (Level: {Level}, Confirmed: {Confirmed}) was rejected by safety validator.",
                    canonical.Id, canonical.Level, decision.Confirmed);
                rejectedOrIgnored.Add(decision.SuggestionId);
                continue;
            }

            var origin = canonical.Level == OptimizationSafetyLevel.Safe
                ? "CurriculoOriginal"
                : "DeclaradaPeloUsuario";

            var userDecl = string.IsNullOrWhiteSpace(decision.UserDeclaration) ? null : decision.UserDeclaration.Trim();
            var effectiveProposed = !string.IsNullOrWhiteSpace(userDecl) && canonical.Level == OptimizationSafetyLevel.NeedsConfirmation
                ? userDecl
                : canonical.ProposedText;

            appliedItems.Add(new AppliedOptimizationItemModel(
                canonical.Id,
                canonical.Level,
                canonical.OriginalText,
                effectiveProposed,
                canonical.Reason,
                decision.Confirmed,
                origin,
                userDecl));
        }

        var adaptedText = ComposeAdaptedText(entity.OriginalText, appliedItems);

        entity.Status = "Applied";
        entity.Version += 1;
        entity.DecisionsJson = JsonSerializer.Serialize(new AppliedOptimizationSnapshot(command.Decisions, appliedItems), JsonOptions);
        entity.AdaptedText = adaptedText;
        entity.UpdatedAt = clock.GetUtcNow();

        await optimizations.UpdateAsync(entity, cancellationToken);

        _logger.LogInformation(
            "Successfully applied {Count} optimizations for OptimizationId {OptimizationId}",
            appliedItems.Count, entity.Id);

        return new OptimizationResultModel(
            entity.Id,
            entity.AnalysisId,
            entity.ResumeId,
            entity.OriginalText,
            adaptedText,
            appliedItems,
            entity.UpdatedAt);
    }

    private sealed record AppliedOptimizationSnapshot(
        IReadOnlyList<OptimizationDecisionModel> Decisions,
        IReadOnlyList<AppliedOptimizationItemModel> AppliedItems);

    private static string ComposeAdaptedText(string originalText, IReadOnlyList<AppliedOptimizationItemModel> appliedItems)
    {
        var result = originalText;
        var additions = new List<string>();

        foreach (var item in appliedItems)
        {
            if (!string.IsNullOrWhiteSpace(item.OriginalText) && result.Contains(item.OriginalText, StringComparison.OrdinalIgnoreCase))
            {
                var index = result.IndexOf(item.OriginalText, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    result = string.Concat(
                        result.AsSpan(0, index),
                        item.ProposedText,
                        result.AsSpan(index + item.OriginalText.Length));
                }
            }
            else if (!string.IsNullOrWhiteSpace(item.ProposedText))
            {
                additions.Add(item.ProposedText);
            }
        }

        if (additions.Count > 0)
        {
            result = $"{result.TrimEnd()}\n\n---\n[Informações e Competências Adicionais Confirmadas]:\n" +
                     string.Join("\n", additions.Select(a => $"• {a}"));
        }

        return result;
    }

    private static bool AreDecisionsEquivalent(
        IReadOnlyList<OptimizationDecisionModel> first,
        IReadOnlyList<OptimizationDecisionModel> second)
    {
        if (first.Count != second.Count)
            return false;

        var firstMap = first.ToDictionary(x => x.SuggestionId);
        foreach (var item in second)
        {
            if (!firstMap.TryGetValue(item.SuggestionId, out var existing))
                return false;
            if (existing.Accepted != item.Accepted || existing.Confirmed != item.Confirmed ||
                !string.Equals(existing.UserDeclaration?.Trim(), item.UserDeclaration?.Trim(), StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
