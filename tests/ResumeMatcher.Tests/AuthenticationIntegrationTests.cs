using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ResumeMatcher.Api;

namespace ResumeMatcher.Tests;

public sealed class AuthenticationIntegrationTests
{
    [Theory]
    [InlineData("GET", "/health")]
    [InlineData("GET", "/api/auth/session")]
    [InlineData("POST", "/api/resumes/upload")]
    [InlineData("POST", "/api/analysis/compare")]
    [InlineData("GET", "/api/analysis/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/resumes/00000000-0000-0000-0000-000000000001")]
    public async Task EndpointsRequireAuthentication(string method, string path)
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.LlmCallCount);
    }

    [Fact]
    public async Task AuthorizedGoogleUserCanValidateSession()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        using var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("future-auth")]
    [InlineData("empty-subject")]
    public async Task InvalidTokensAreRejected(string scenario)
    {
        var token = scenario switch
        {
            "expired" => ResumeMatcherApiFactory.CreateToken(expired: true),
            "signature" => ResumeMatcherApiFactory.CreateToken(validSignature: false),
            "issuer" => ResumeMatcherApiFactory.CreateToken(issuer: "https://attacker.example.test"),
            "audience" => ResumeMatcherApiFactory.CreateToken(audience: "another-project"),
            "future-auth" => ResumeMatcherApiFactory.CreateToken(futureAuth: true),
            "empty-subject" => ResumeMatcherApiFactory.CreateToken(emptySubject: true),
            _ => "not-a-jwt"
        };
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("other@example.test", true, "google.com")]
    [InlineData(ResumeMatcherApiFactory.AllowedEmail, false, "google.com")]
    [InlineData(ResumeMatcherApiFactory.AllowedEmail, true, "password")]
    [InlineData(ResumeMatcherApiFactory.AllowedEmail, true, "anonymous")]
    public async Task UnapprovedIdentitiesCannotReachApplication(string email, bool verified, string provider)
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            ResumeMatcherApiFactory.CreateToken(email: email, verified: verified, provider: provider));
        using var response = await client.PostAsync("/api/analysis/compare", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, factory.LlmCallCount);
    }

    [Theory]
    [InlineData("/api/resumes/upload")]
    [InlineData("/api/analysis/compare")]
    public async Task AllowedOriginPreflightWorksWithoutToken(string path)
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", "https://resume-matcher-f61df.web.app");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://resume-matcher-f61df.web.app", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("authorization", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers")), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, factory.LlmCallCount);
    }

    [Fact]
    public async Task UnauthorizedResponseStillHasCorsHeadersForFrontend()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", "https://resume-matcher-f61df.web.app");
        using var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task UnknownOriginDoesNotReceiveCorsPermission()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        client.DefaultRequestHeaders.Add("Origin", "https://untrusted.example.test");
        using var response = await client.GetAsync("/api/auth/session");
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public void MissingAllowedAccountFailsAtStartup()
    {
        using var factory = new ResumeMatcherApiFactory();
        using var unconfigured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.PostConfigure<FirebaseAuthOptions>(options => options.AllowedEmail = "")));
        Assert.Throws<OptionsValidationException>(() => unconfigured.CreateClient());
    }
}
