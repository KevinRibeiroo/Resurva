using ResumeMatcher.Application;
using Xunit;

namespace ResumeMatcher.Tests;

public sealed class ChronologyValidatorTests
{
    private readonly DateOnly _referenceDate = new(2026, 9, 4);

    [Fact]
    public void ValidatePeriod_StartInPastWithAtual_IsValidAndNotFuture()
    {
        // 05/2026 is before reference date 2026-09-04
        var result = ChronologyValidator.ValidatePeriod("05/2026 - Atual", _referenceDate);

        Assert.True(result.IsValid);
        Assert.False(result.IsFuture);
        Assert.False(result.IsIndeterminate);
    }

    [Fact]
    public void ValidatePeriod_StartInFutureWithAtual_IsFutureAndNotValid()
    {
        // 10/2026 is after reference date 2026-09-04
        var result = ChronologyValidator.ValidatePeriod("10/2026 - Atual", _referenceDate);

        Assert.False(result.IsValid);
        Assert.True(result.IsFuture);
        Assert.False(result.IsIndeterminate);
    }

    [Fact]
    public void ValidatePeriod_YearInFuture_IsFuture()
    {
        var result = ChronologyValidator.ValidatePeriod("2027 - Atual", _referenceDate);

        Assert.False(result.IsValid);
        Assert.True(result.IsFuture);
    }

    [Fact]
    public void ValidatePeriod_CompletedPastPeriod_IsValid()
    {
        var result = ChronologyValidator.ValidatePeriod("01/2024 - 05/2026", _referenceDate);

        Assert.True(result.IsValid);
        Assert.False(result.IsFuture);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Período não informado")]
    public void ValidatePeriod_IncompleteOrMissing_IsIndeterminateAndDoesNotHallucinate(string? text)
    {
        var result = ChronologyValidator.ValidatePeriod(text, _referenceDate);

        Assert.True(result.IsValid);
        Assert.False(result.IsFuture);
        Assert.True(result.IsIndeterminate);
    }

    [Fact]
    public void ValidatePeriod_StartAfterEnd_IsInvalid()
    {
        var result = ChronologyValidator.ValidatePeriod("08/2025 - 01/2025", _referenceDate);

        Assert.False(result.IsValid);
        Assert.False(result.IsFuture);
        Assert.False(result.IsIndeterminate);
    }
}
