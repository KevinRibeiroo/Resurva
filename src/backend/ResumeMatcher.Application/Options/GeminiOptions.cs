namespace ResumeMatcher.Application;

public sealed class GeminiOptions
{
    public const string SectionName = "LLM";

    public string Provider { get; set; } = "Mock";
    public string Model { get; set; } = "gemini-3.5-flash";
    public string? ApiKey { get; set; }
    public string? ProjectId { get; set; }
    public string Location { get; set; } = "southamerica-east1";
    public double Temperature { get; set; } = 0.1;
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxRetries { get; set; } = 1;
    public int RetryBaseDelayMilliseconds { get; set; } = 500;
}
