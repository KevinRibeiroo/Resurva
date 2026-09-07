using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ResumeMatcher.Api;

public sealed class PrivateUserAuthorizationHandler(
    IOptions<FirebaseAuthOptions> options,
    ILogger<PrivateUserAuthorizationHandler>? logger = null)
    : AuthorizationHandler<PrivateUserRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PrivateUserRequirement requirement)
    {
        var email = context.User.FindFirst("email")?.Value;
        var verified = context.User.FindFirst("email_verified")?.Value;
        var firebase = context.User.FindFirst("firebase")?.Value;

        if (context.User.Identity?.IsAuthenticated != true)
        {
            logger?.LogWarning("Autorização recusada: identidade não autenticada.");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(context.User.FindFirst("sub")?.Value))
        {
            logger?.LogWarning("Autorização recusada: claim 'sub' ausente no token.");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(options.Value.AllowedEmail))
        {
            logger?.LogError("Autorização recusada: Authentication:Firebase:AllowedEmail não está configurado na aplicação.");
            return Task.CompletedTask;
        }

        if (!string.Equals(email, options.Value.AllowedEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning("Autorização recusada: conta fora da lista de acesso.");
            return Task.CompletedTask;
        }

        if (!string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogWarning("Autorização recusada: e-mail não verificado.");
            return Task.CompletedTask;
        }

        if (firebase is null)
        {
            logger?.LogWarning("Autorização recusada: claim 'firebase' ausente.");
            return Task.CompletedTask;
        }

        try
        {
            using var metadata = JsonDocument.Parse(firebase);
            if (metadata.RootElement.ValueKind == JsonValueKind.Object &&
                metadata.RootElement.TryGetProperty("sign_in_provider", out var provider) &&
                provider.ValueKind == JsonValueKind.String && provider.GetString() == "google.com")
            {
                logger?.LogInformation("Acesso autorizado ao ambiente privado.");
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            logger?.LogWarning("Autorização recusada: sign_in_provider inválido.");
        }
        catch (JsonException)
        {
            logger?.LogWarning("Autorização recusada: metadados Firebase mal formatados.");
        }

        return Task.CompletedTask;
    }
}
