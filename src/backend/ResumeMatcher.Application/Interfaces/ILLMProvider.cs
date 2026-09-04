namespace ResumeMatcher.Application;

public interface ILLMProvider
{
    string ModelName { get; }
    string PromptVersion { get; }

    Task<StructuredComparisonModel> CompareAsync(
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken);
}
