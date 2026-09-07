namespace ResumeMatcher.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RESUMEMATCHER_TEST_POSTGRES")))
            Skip = "Set RESUMEMATCHER_TEST_POSTGRES to a disposable local PostgreSQL instance. CI always supplies it.";
    }
}
