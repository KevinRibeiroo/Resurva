using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public interface IResumeOptimizationProvider
{
    string ModelName { get; }
    string ConfigurationFingerprint { get; }
    string PromptVersion { get; }

    Task<IReadOnlyList<OptimizationSuggestionModel>> GenerateSuggestionsAsync(
        string resumeText,
        string jobDescription,
        StructuredComparisonModel comparisonContext,
        CancellationToken cancellationToken);
}
