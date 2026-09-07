using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using ResumeMatcher.Api;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;
using ResumeMatcher.Infrastructure;

namespace ResumeMatcher.Tests;

public sealed class ResumeMatcherApiFactory : WebApplicationFactory<Program>
{
    public const string FirebaseProjectId = "resume-tests";
    public const string AllowedEmail = "owner@example.test";
    private static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048)) { KeyId = "test-signing-key" };
    private static readonly RsaSecurityKey OtherSigningKey = new(RSA.Create(2048)) { KeyId = "test-signing-key" };
    private readonly string _databaseName = $"resume-matcher-tests-{Guid.NewGuid():N}";
    private readonly CountingLLMProvider _llmProvider = new();

    public int LlmCallCount => _llmProvider.CallCount;
    public TestTimeProvider Clock { get; } = new();

    public HttpClient CreateAuthenticatedClient(string subject = "synthetic-owner-uid")
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(subject: subject));
        return client;
    }

    public static string CreateToken(string email = AllowedEmail, bool verified = true,
        string? issuer = null, string? audience = null, bool expired = false,
        bool validSignature = true, string provider = "google.com", bool futureAuth = false, bool emptySubject = false,
        string subject = "synthetic-owner-uid")
    {
        var now = DateTime.UtcNow;
        var issuedAt = expired ? now.AddHours(-2) : now.AddMinutes(-1);
        var payload = new JwtPayload(
            issuer ?? $"https://securetoken.google.com/{FirebaseProjectId}",
            audience ?? FirebaseProjectId, null, issuedAt,
            expired ? now.AddHours(-1) : now.AddHours(1), issuedAt)
        {
            ["sub"] = emptySubject ? "" : subject,
            ["email"] = email,
            ["email_verified"] = verified,
            ["auth_time"] = new DateTimeOffset(futureAuth ? now.AddHours(1) : issuedAt).ToUnixTimeSeconds(),
            ["firebase"] = new Dictionary<string, object> { ["sign_in_provider"] = provider }
        };
        var header = new JwtHeader(new SigningCredentials(validSignature ? SigningKey : OtherSigningKey, SecurityAlgorithms.RsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders().AddConsole());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.Configure<FirebaseAuthOptions>(options =>
            {
                options.ProjectId = FirebaseProjectId;
                options.AllowedEmail = AllowedEmail;
            });
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration
                {
                    Issuer = $"https://securetoken.google.com/{FirebaseProjectId}"
                };
                configuration.SigningKeys.Add(SigningKey);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });
            var descriptorsToRemove = services
                .Where(d => d.ServiceType.Namespace?.StartsWith("Npgsql") == true
                         || d.ImplementationType?.Namespace?.StartsWith("Npgsql") == true
                         || d.ServiceType.FullName?.Contains("ResumeMatcherDbContext") == true
                         || d.ServiceType == typeof(DbContextOptions)
                         || d.ServiceType == typeof(DbContextOptions<ResumeMatcherDbContext>)
                         || d.ServiceType == typeof(ResumeMatcherDbContext))
                .ToList();
            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            var inMemoryServiceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();

            services.AddDbContext<ResumeMatcherDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.UseInternalServiceProvider(inMemoryServiceProvider);
            });

            services.RemoveAll<ILLMProvider>();
            services.AddSingleton<ILLMProvider>(_llmProvider);
        });
    }

    private sealed class CountingLLMProvider : ILLMProvider
    {
        private int _callCount;

        public int CallCount => _callCount;
        public string ModelName => "integration-test-model";
        public string ConfigurationFingerprint => "default";
        public string PromptVersion => "v1";

        public Task<StructuredComparisonModel> CompareAsync(
            string resumeText,
            string jobDescription,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(new StructuredComparisonModel(
                MatchedSkills: [new EvidenceItemModel("C#", "experiência com C#")],
                MissingSkills: [],
                RequirementsMet: [new EvidenceItemModel("Backend", "desenvolvimento backend")],
                RequirementsMissing: [],
                Strengths: [new EvidenceItemModel("Experiência backend")],
                PointsOfAttention: [],
                Recommendations: [],
                ExperienceMatch: 90,
                SeniorityMatch: 80,
                EducationMatch: 70));
        }
    }
}
