namespace ResumeMatcher.Api;

public sealed class ApiRateLimitOptions
{
    public const string SectionName = "RateLimiting";
    public const string PolicyName = "api";

    public int PermitLimit { get; set; } = 20;
    public int WindowSeconds { get; set; } = 60;
}
