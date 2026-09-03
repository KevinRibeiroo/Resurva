using Microsoft.AspNetCore.Mvc;
using ResumeMatcher.Application;

namespace ResumeMatcher.Api;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var status = exception switch
            {
                ResourceNotFoundException => StatusCodes.Status404NotFound,
                UnsupportedResumeFormatException => StatusCodes.Status415UnsupportedMediaType,
                InvalidResumeException or ArgumentException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };
            if (status >= 500)
                logger.LogError(exception, "Unhandled request error");
            else
                logger.LogWarning(exception, "Request validation error");
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = status >= 500 ? "An unexpected error occurred." : exception.Message
            });
        }
    }
}
