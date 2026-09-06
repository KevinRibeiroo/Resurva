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
                logger.LogInformation(
                    "Iniciando chamada ao Gemini (tentativa {Attempt} de {TotalAttempts}, timeout: {Timeout}s)...",
                    attempt + 1,
                    _options.MaxRetries + 1,
                    _options.TimeoutSeconds);

                return await operation(timeoutSource.Token);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(
                    exception,
                    "Timeout na chamada ao Gemini após {Timeout}s na tentativa {Attempt} de {TotalAttempts}",
                    _options.TimeoutSeconds,
                    attempt + 1,
                    _options.MaxRetries + 1);

                if (attempt >= _options.MaxRetries)
                    throw new LLMProviderTimeoutException("The Gemini request timed out.", exception);

                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (Exception exception) when (IsTransient(exception))
            {
                logger.LogWarning(
                    exception,
                    "Falha transitória na chamada ao Gemini (tentativa {Attempt} de {TotalAttempts}): {ExceptionType} - {Message}. Causa interna: {InnerMessage}",
                    attempt + 1,
                    _options.MaxRetries + 1,
                    exception.GetType().Name,
                    exception.Message,
                    exception.InnerException?.Message ?? "Nenhuma");

                if (attempt >= _options.MaxRetries)
                {
                    logger.LogError(
                        exception,
                        "Tentativas de chamada ao Gemini esgotadas após {TotalAttempts} tentativas. Erro final: {Message}",
                        _options.MaxRetries + 1,
                        exception.Message);
                    throw new LLMProviderUnavailableException($"Gemini is temporarily unavailable: {exception.Message}", exception);
                }

                await DelayBeforeRetryAsync(attempt, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Falha permanente/não-transitória na chamada ao Gemini na tentativa {Attempt}: {ExceptionType} - {Message}. Causa interna: {InnerMessage}",
                    attempt + 1,
                    exception.GetType().Name,
                    exception.Message,
                    exception.InnerException?.Message ?? "Nenhuma");
                throw;
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
        if (exception is HttpRequestException httpException)
        {
            if (httpException.StatusCode.HasValue)
            {
                return httpException.StatusCode.Value is
                    HttpStatusCode.RequestTimeout or
                    HttpStatusCode.TooManyRequests or
                    HttpStatusCode.InternalServerError or
                    HttpStatusCode.BadGateway or
                    HttpStatusCode.ServiceUnavailable or
                    HttpStatusCode.GatewayTimeout;
            }

            return true;
        }

        return exception is GoogleApiException googleException && googleException.HttpStatusCode is
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.InternalServerError or
            HttpStatusCode.BadGateway or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout;
    }
}
