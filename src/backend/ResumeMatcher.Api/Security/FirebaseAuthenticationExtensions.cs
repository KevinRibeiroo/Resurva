using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ResumeMatcher.Api;

public static class FirebaseAuthenticationExtensions
{
    public static IServiceCollection AddFirebaseAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FirebaseAuthOptions>()
            .Bind(configuration.GetSection(FirebaseAuthOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.ProjectId) &&
                options.ProjectId.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'),
                "Authentication:Firebase:ProjectId is required and must be a Firebase project ID.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AllowedEmail) &&
                System.Net.Mail.MailAddress.TryCreate(options.AllowedEmail.Trim(), out var address) &&
                address.Address == options.AllowedEmail.Trim(),
                "Authentication:Firebase:AllowedEmail must contain the single email authorized for this private environment.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<FirebaseAuthOptions>>((options, firebase) =>
            {
                var issuer = $"https://securetoken.google.com/{firebase.Value.ProjectId}";
                options.Authority = issuer;
                options.Audience = firebase.Value.ProjectId;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.SaveToken = false;
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var subject = context.Principal?.FindFirst("sub")?.Value;
                        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 128 ||
                            !long.TryParse(context.Principal?.FindFirst("iat")?.Value, out var issuedAt) || issuedAt > now ||
                            !long.TryParse(context.Principal?.FindFirst("auth_time")?.Value, out var authenticatedAt) || authenticatedAt > now)
                            context.Fail("Invalid Firebase token claims.");
                        return Task.CompletedTask;
                    }
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = firebase.Value.ProjectId,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddSingleton<IAuthorizationHandler, PrivateUserAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            var policy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new PrivateUserRequirement())
                .Build();
            options.DefaultPolicy = policy;
            options.FallbackPolicy = policy;
        });
        return services;
    }
}
