using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResumeMatcher.Domain;
using ResumeMatcher.Infrastructure;

namespace ResumeMatcher.Tests;

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task HealthEndpointReturnsOk()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CompareEndpointPersistsAndReusesIdenticalAnalysis()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        var resume = await SeedResumeAsync(factory.Services);
        var request = new
        {
            resumeId = resume.Id,
            jobDescription = "Vaga backend com C# e desenvolvimento de APIs."
        };

        var firstResponse = await client.PostAsJsonAsync("/api/analysis/compare", request);
        var secondResponse = await client.PostAsJsonAsync("/api/analysis/compare", request);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<AnalysisResultModel>();
        var second = await secondResponse.Content.ReadFromJsonAsync<AnalysisResultModel>();
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(1, factory.LlmCallCount);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
        Assert.Equal(1, await db.Analyses.CountAsync());
    }

    [Theory]
    [InlineData(75_000, HttpStatusCode.Created, 1)]
    [InlineData(75_001, HttpStatusCode.BadRequest, 0)]
    public async Task CompareEndpointEnforcesJobDescriptionLimit(int length, HttpStatusCode expectedStatus, int expectedLlmCalls)
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        var resume = await SeedResumeAsync(factory.Services);
        var request = new
        {
            resumeId = resume.Id,
            jobDescription = new string('x', length)
        };

        var response = await client.PostAsJsonAsync("/api/analysis/compare", request);

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(expectedLlmCalls, factory.LlmCallCount);
    }

    [Fact]
    public async Task ApiRateLimitRejectsRequestsAboveConfiguredWindowLimit()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        for (var requestNumber = 0; requestNumber < 20; requestNumber++)
        {
            var response = await client.GetAsync($"/api/analysis/{Guid.NewGuid()}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        var limitedResponse = await client.GetAsync($"/api/analysis/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteResumeRemovesItsPersistedAnalyses()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        var resume = await SeedResumeAsync(factory.Services);
        var compareResponse = await client.PostAsJsonAsync("/api/analysis/compare", new
        {
            resumeId = resume.Id,
            jobDescription = "Vaga backend com C# e APIs."
        });
        var analysis = await compareResponse.Content.ReadFromJsonAsync<AnalysisResultModel>();
        Assert.NotNull(analysis);

        var deleteResponse = await client.DeleteAsync($"/api/resumes/{resume.Id}");
        var getAnalysisResponse = await client.GetAsync($"/api/analysis/{analysis.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getAnalysisResponse.StatusCode);
    }

    private static async Task<ResumeEntity> SeedResumeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
        var resume = new ResumeEntity
        {
            FileName = "integration-test.pdf",
            ContentType = "application/pdf",
            ExtractedText = "Desenvolvedor backend com experiência em C# e APIs."
        };
        db.Resumes.Add(resume);
        await db.SaveChangesAsync();
        return resume;
    }
}
