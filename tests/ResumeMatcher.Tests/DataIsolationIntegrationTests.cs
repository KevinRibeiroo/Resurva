using System.Net;
using System.Net.Http.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;
using ResumeMatcher.Infrastructure;

namespace ResumeMatcher.Tests;

public sealed class DataIsolationIntegrationTests
{
    [Fact]
    public async Task UploadUsesTokenSubjectAndIgnoresSubmittedOwner()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient("user-a");
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        {
            document.AddMainDocumentPart().Document = new Document(new Body(new Paragraph(new Run(new Text("Synthetic developer with C# skills.")))));
        }
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(stream.ToArray());
        file.Headers.ContentType = new("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        form.Add(file, "file", "synthetic.docx");
        form.Add(new StringContent("user-b"), "ownerUserId");
        using var response = await client.PostAsync("/api/resumes/upload", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var resume = await scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>().Resumes.SingleAsync();
        Assert.Equal("user-a", resume.OwnerUserId);
        Assert.Equal(factory.Clock.GetUtcNow(), resume.UpdatedAt);
    }

    [Fact]
    public async Task AnotherSubjectCannotReadCompareOrDeleteOwnedData()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var a = factory.CreateAuthenticatedClient("user-a");
        using var b = factory.CreateAuthenticatedClient("user-b");
        var resume = await SeedAsync(factory, "user-a");
        var analysis = await CompareAsync(a, resume.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/analysis/{analysis.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync("/api/analysis/compare", new { resumeId = resume.Id, jobDescription = "C#", ownerUserId = "user-a" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/resumes/{resume.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/analysis/{analysis.Id}")).StatusCode);
        Assert.Equal(1, factory.LlmCallCount);
    }

    [Fact]
    public async Task IdenticalInputsDoNotSharePrivateCacheAcrossSubjects()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var a = factory.CreateAuthenticatedClient("user-a");
        using var b = factory.CreateAuthenticatedClient("user-b");
        var ra = await SeedAsync(factory, "user-a");
        var rb = await SeedAsync(factory, "user-b");
        var results = await Task.WhenAll(CompareAsync(a, ra.Id), CompareAsync(b, rb.Id));
        Assert.NotEqual(results[0].Id, results[1].Id);
        Assert.Equal(ra.Id, results[0].ResumeId);
        Assert.Equal(rb.Id, results[1].ResumeId);
        Assert.Equal(2, factory.LlmCallCount);
        Assert.Equal(results[0].Id, (await CompareAsync(a, ra.Id)).Id);
        Assert.Equal(results[1].Id, (await CompareAsync(b, rb.Id)).Id);
        Assert.Equal(2, factory.LlmCallCount);
        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/api/resumes/{ra.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync($"/api/analysis/{results[1].Id}")).StatusCode);
    }

    [Fact]
    public async Task UnassignedLegacyDataIsNotAutomaticallyClaimed()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        var resume = await SeedAsync(factory, "");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/analysis/compare", new { resumeId = resume.Id, jobDescription = "C#" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/resumes/{resume.Id}")).StatusCode);
        Assert.Equal(0, factory.LlmCallCount);
    }

    [Fact]
    public async Task ReadsAndCacheHitsDoNotExtendThirtyDayRetention()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        var resume = await SeedAsync(factory, "synthetic-owner-uid");
        var analysis = await CompareAsync(client, resume.Id);
        factory.Clock.Advance(TimeSpan.FromDays(29));
        Assert.Equal(analysis.Id, (await CompareAsync(client, resume.Id)).Id);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/analysis/{analysis.Id}")).StatusCode);
        factory.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/analysis/{analysis.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/analysis/compare", new { resumeId = resume.Id, jobDescription = "Another job" })).StatusCode);
        Assert.Equal(1, factory.LlmCallCount);
        await using var scope = factory.Services.CreateAsyncScope();
        var cleanup = scope.ServiceProvider.GetRequiredService<RetentionCleanupService>();
        Assert.Equal(2, await cleanup.PurgeExpiredAsync(CancellationToken.None));
        Assert.Equal(0, await cleanup.PurgeExpiredAsync(CancellationToken.None));
    }

    [Fact]
    public async Task NewAnalysisUpdatesResumeButNotPreviousAnalyses()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();
        var resume = await SeedAsync(factory, "synthetic-owner-uid");
        var first = await CompareAsync(client, resume.Id);
        factory.Clock.Advance(TimeSpan.FromDays(29));
        var second = await CompareAsync(client, resume.Id, "Different job C#");
        factory.Clock.Advance(TimeSpan.FromDays(1));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/analysis/{first.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/analysis/{second.Id}")).StatusCode);
        var replacement = await CompareAsync(client, resume.Id);
        Assert.NotEqual(first.Id, replacement.Id);
        Assert.Equal(3, factory.LlmCallCount);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<RetentionCleanupService>().PurgeExpiredAsync(CancellationToken.None));
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>().Resumes.CountAsync());
    }

    internal static async Task<ResumeEntity> SeedAsync(ResumeMatcherApiFactory factory, string owner)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
        var resume = new ResumeEntity { OwnerUserId = owner, FileName = "synthetic.pdf", ContentType = "application/pdf", ExtractedText = "Synthetic C# developer.", UpdatedAt = factory.Clock.GetUtcNow() };
        db.Resumes.Add(resume);
        await db.SaveChangesAsync();
        return resume;
    }

    private static async Task<AnalysisResultModel> CompareAsync(HttpClient client, Guid resumeId, string jobDescription = "C# backend job")
    {
        using var response = await client.PostAsJsonAsync("/api/analysis/compare", new { resumeId, jobDescription });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AnalysisResultModel>())!;
    }
}
