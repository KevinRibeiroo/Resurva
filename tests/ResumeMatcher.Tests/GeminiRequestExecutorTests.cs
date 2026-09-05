using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResumeMatcher.Application;
using ResumeMatcher.Infrastructure;

namespace ResumeMatcher.Tests;

public sealed class GeminiRequestExecutorTests
{
    [Fact]
    public async Task ExecuteAsyncRetriesOneTransientFailure()
    {
        var options = Options.Create(new GeminiOptions
        {
            MaxRetries = 1,
            RetryBaseDelayMilliseconds = 1,
            TimeoutSeconds = 10
        });
        var executor = new GeminiRequestExecutor(options, NullLogger<GeminiRequestExecutor>.Instance);
        var attempts = 0;

        var result = await executor.ExecuteAsync<int>(
            _ => ++attempts == 1
                ? Task.FromException<int>(new HttpRequestException("temporary"))
                : Task.FromResult(42),
            CancellationToken.None);

        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsyncMapsExhaustedTransientFailureToUnavailableException()
    {
        var options = Options.Create(new GeminiOptions
        {
            MaxRetries = 0,
            TimeoutSeconds = 10
        });
        var executor = new GeminiRequestExecutor(options, NullLogger<GeminiRequestExecutor>.Instance);

        await Assert.ThrowsAsync<LLMProviderUnavailableException>(() => executor.ExecuteAsync<int>(
            _ => Task.FromException<int>(new HttpRequestException("temporary")),
            CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsyncPreservesCallerCancellation()
    {
        var executor = new GeminiRequestExecutor(
            Options.Create(new GeminiOptions { MaxRetries = 1, TimeoutSeconds = 10 }),
            NullLogger<GeminiRequestExecutor>.Instance);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync<int>(
            token => Task.Delay(TimeSpan.FromSeconds(1), token).ContinueWith(_ => 1, token),
            cancellationSource.Token));
    }
}
