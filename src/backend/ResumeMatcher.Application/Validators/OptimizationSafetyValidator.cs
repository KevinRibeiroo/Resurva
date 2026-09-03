namespace ResumeMatcher.Application;

public sealed class OptimizationSafetyValidator : IOptimizationSafetyValidator
{
    public bool CanApply(OptimizationSuggestionModel suggestion)
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
