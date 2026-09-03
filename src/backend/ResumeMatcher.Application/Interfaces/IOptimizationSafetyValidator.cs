namespace ResumeMatcher.Application;

public interface IOptimizationSafetyValidator
{
    bool CanApply(OptimizationSuggestionModel suggestion);
}
