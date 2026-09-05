using System.Net;
using Google;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ResumeMatcher.Application;

namespace ResumeMatcher.Infrastructure;

public sealed class GeminiRequestExecutor(
    IOptions<GeminiOptions> options,
    ILogger<GeminiRequestExecutor> logger)
{
    private readonly GeminiOptions _options = options.Value;

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        for (var attempt = 0; ; attempt++)
        {
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            try
            {
                return await operation(timeoutSource.Token);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt >= _options.MaxRetries)
                    throw new LLMProviderTimeoutException("The Gemini request timed out.", exception);

                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                if (attempt >= _options.MaxRetries)
                    throw new LLMProviderUnavailableException("Gemini is temporarily unavailable.", exception);

                logger.LogWarning(
                    "Transient Gemini failure. Retrying request after attempt {Attempt} of {TotalAttempts}",
                    attempt + 1,
                    _options.MaxRetries + 1);
                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
        }
    }

    private Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var multiplier = Math.Pow(2, attempt);
        var delay = TimeSpan.FromMilliseconds(_options.RetryBaseDelayMilliseconds * multiplier);
        return Task.Delay(delay, cancellationToken);
    }

    private static bool IsTransient(Exception exception)
    {
        if (exception is HttpRequestException)
            return true;

        return exception is GoogleApiException googleException && googleException.HttpStatusCode is
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;
    }
}
