using ResumeMatcher.Application;

namespace ResumeMatcher.Tests;

public sealed class OptimizationSafetyValidatorTests
{
    private readonly OptimizationSafetyValidator _validator = new();

    [Fact]
    public void Safe_is_allowed()
    {
        Assert.True(_validator.CanApply(new OptimizationSuggestionModel(Guid.NewGuid(), OptimizationSafetyLevel.Safe)));
    }

    [Fact]
    public void Needs_confirmation_is_blocked_without_confirmation()
    {
        Assert.False(_validator.CanApply(new OptimizationSuggestionModel(Guid.NewGuid(), OptimizationSafetyLevel.NeedsConfirmation)));
    }

    [Fact]
    public void Needs_confirmation_is_allowed_after_confirmation()
    {
        Assert.True(_validator.CanApply(new OptimizationSuggestionModel(Guid.NewGuid(), OptimizationSafetyLevel.NeedsConfirmation, true)));
    }

    [Fact]
    public void Forbidden_is_always_blocked()
    {
        Assert.False(_validator.CanApply(new OptimizationSuggestionModel(Guid.NewGuid(), OptimizationSafetyLevel.Forbidden, true)));
    }
}
