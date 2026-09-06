using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ResumeMatcher.Api;

public sealed class PrivateUserAuthorizationHandler(IOptions<FirebaseAuthOptions> options)
    : AuthorizationHandler<PrivateUserRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PrivateUserRequirement requirement)
    {
        var email = context.User.FindFirst("email")?.Value;
        var verified = context.User.FindFirst("email_verified")?.Value;
        var firebase = context.User.FindFirst("firebase")?.Value;
        if (context.User.Identity?.IsAuthenticated != true ||
            string.IsNullOrWhiteSpace(context.User.FindFirst("sub")?.Value) ||
            string.IsNullOrWhiteSpace(options.Value.AllowedEmail) ||
            !string.Equals(email, options.Value.AllowedEmail.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(verified, "true", StringComparison.OrdinalIgnoreCase) ||
            firebase is null)
            return Task.CompletedTask;

        try
        {
            using var metadata = JsonDocument.Parse(firebase);
            if (metadata.RootElement.ValueKind == JsonValueKind.Object &&
                metadata.RootElement.TryGetProperty("sign_in_provider", out var provider) &&
                provider.ValueKind == JsonValueKind.String && provider.GetString() == "google.com")
                context.Succeed(requirement);
        }
        catch (JsonException)
        {
            // Malformed provider metadata never authorizes access.
        }

        return Task.CompletedTask;
    }
}
