using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ResumeMatcher.Application;

namespace ResumeMatcher.Api;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.Value;

        logger.LogInformation("Recebendo requisição HTTP {Method} {Path}", method, path);

        try
        {
            await next(context);
            stopwatch.Stop();
            logger.LogInformation("Requisição HTTP {Method} {Path} concluída com status {StatusCode} em {ElapsedMs}ms",
                method, path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            var status = exception switch
            {
                ResourceNotFoundException => StatusCodes.Status404NotFound,
                UnsupportedResumeFormatException => StatusCodes.Status415UnsupportedMediaType,
                OptimizationConflictException => StatusCodes.Status409Conflict,
                InvalidResumeException or ArgumentException => StatusCodes.Status400BadRequest,
                LLMProviderResponseException => StatusCodes.Status502BadGateway,
                LLMProviderUnavailableException => StatusCodes.Status503ServiceUnavailable,
                LLMProviderTimeoutException => StatusCodes.Status504GatewayTimeout,
                _ => StatusCodes.Status500InternalServerError
            };

            if (status >= 500)
            {
                logger.LogError(exception,
                    "Falha ao processar requisição HTTP {Method} {Path} com status {StatusCode} em {ElapsedMs}ms: {ErrorMessage}. Causa interna: {InnerError}",
                    method, path, status, stopwatch.ElapsedMilliseconds, exception.Message, exception.InnerException?.Message ?? "Nenhuma");
            }
            else
            {
                logger.LogWarning(exception,
                    "Requisição HTTP {Method} {Path} rejeitada com validação status {StatusCode} em {ElapsedMs}ms: {ErrorMessage}",
                    method, path, status, stopwatch.ElapsedMilliseconds, exception.Message);
            }

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = status >= 500 ? "An unexpected error occurred." : exception.Message
            });
        }
    }
}
