using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public interface IResumeTextExtractor
{
    bool CanExtract(string extension, string contentType);
    Task<string> ExtractAsync(Stream stream, CancellationToken cancellationToken);
}

public interface IResumeRepository
{
    Task AddAsync(Resume resume, CancellationToken cancellationToken);
    Task<Resume?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public interface IAnalysisRepository
{
    Task AddAsync(Analysis analysis, CancellationToken cancellationToken);
    Task<Analysis?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public interface ILLMProvider
{
    Task<StructuredComparison> CompareAsync(string resumeText, string jobDescription, CancellationToken cancellationToken);
}

public sealed record StructuredComparison(
    IReadOnlyList<EvidenceItem> MatchedSkills,
    IReadOnlyList<EvidenceItem> MissingSkills,
    IReadOnlyList<EvidenceItem> RequirementsMet,
    IReadOnlyList<EvidenceItem> RequirementsMissing,
    IReadOnlyList<EvidenceItem> Strengths,
    IReadOnlyList<EvidenceItem> PointsOfAttention,
    IReadOnlyList<EvidenceItem> Recommendations,
    double ExperienceMatch,
    double SeniorityMatch,
    double EducationMatch);

public sealed record ScoreComponents(
    double Skills,
    double Experience,
    double Seniority,
    double Requirements,
    double Education);

public sealed record ScoreBreakdown(
    double Overall,
    double Skills,
    double Experience,
    double Seniority,
    double Requirements,
    double Education);

public interface IScoringEngine
{
    ScoreBreakdown Calculate(ScoreComponents components);
}

public sealed class ScoringOptions
{
    public const string SectionName = "Scoring";
    public double SkillsWeight { get; set; } = 0.40;
    public double ExperienceWeight { get; set; } = 0.30;
    public double SeniorityWeight { get; set; } = 0.15;
    public double RequirementsWeight { get; set; } = 0.10;
    public double EducationWeight { get; set; } = 0.05;
}

public sealed record UploadResumeCommand(string FileName, string ContentType, Stream Content);
public sealed record UploadResumeResult(Guid Id, string FileName, int ExtractedCharacters);
public sealed record CompareCommand(Guid ResumeId, string JobDescription);

public interface IResumeService
{
    Task<UploadResumeResult> UploadAsync(UploadResumeCommand command, CancellationToken cancellationToken);
}

public interface IAnalysisService
{
    Task<AnalysisResult> CompareAsync(CompareCommand command, CancellationToken cancellationToken);
    Task<AnalysisResult?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class UnsupportedResumeFormatException(string message) : Exception(message)
{
}

public sealed class InvalidResumeException(string message) : Exception(message)
{
}

public sealed class ResourceNotFoundException(string message) : Exception(message)
{
}

public enum OptimizationSafetyLevel
{
    Safe,
    NeedsConfirmation,
    Forbidden
}

public sealed record OptimizationSuggestion(Guid Id, OptimizationSafetyLevel Level, bool Confirmed = false);

public interface IOptimizationSafetyValidator
{
    bool CanApply(OptimizationSuggestion suggestion);
}
public sealed class OptimizationSafetyValidator : IOptimizationSafetyValidator
{
    public bool CanApply(OptimizationSuggestion suggestion)
    {
        return suggestion.Level switch
        {
            OptimizationSafetyLevel.Safe => true,
            OptimizationSafetyLevel.NeedsConfirmation => suggestion.Confirmed,
            OptimizationSafetyLevel.Forbidden => false,
            _ => false
        };
    }
}
