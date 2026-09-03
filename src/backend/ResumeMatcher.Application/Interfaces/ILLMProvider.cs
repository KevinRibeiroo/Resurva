namespace ResumeMatcher.Application;

public interface ILLMProvider
{
    Task<StructuredComparisonModel> CompareAsync(
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken);
}
