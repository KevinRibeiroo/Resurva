using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ResumeMatcher.Api.Models;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;
using ResumeMatcher.Infrastructure;

namespace ResumeMatcher.Tests;

public sealed class OptimizationIntegrationTests
{
    [Fact]
    public async Task CreatePlan_GetPlan_And_ApplyDecisions_CompleteFlow()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        // 1. Seed resume and perform comparison
        var resume = await SeedResumeAsync(factory.Services);
        var compareResponse = await client.PostAsJsonAsync("/api/analysis/compare", new
        {
            resumeId = resume.Id,
            jobDescription = "Vaga para desenvolvedor C# com experiência em PostgreSQL e arquitetura em nuvem."
        });
        Assert.Equal(HttpStatusCode.Created, compareResponse.StatusCode);
        var analysis = await compareResponse.Content.ReadFromJsonAsync<AnalysisResultModel>();
        Assert.NotNull(analysis);

        // 2. Request optimization plan
        var planResponse = await client.PostAsync($"/api/analysis/{analysis.Id}/optimization", null);
        Assert.Equal(HttpStatusCode.Created, planResponse.StatusCode);
        Assert.NotNull(planResponse.Headers.Location);

        var plan = await planResponse.Content.ReadFromJsonAsync<OptimizationPlanModel>();
        Assert.NotNull(plan);
        Assert.Equal(analysis.Id, plan.AnalysisId);
        Assert.Equal(resume.Id, plan.ResumeId);
        Assert.NotEmpty(plan.Suggestions);

        // 3. GET plan by id
        var getPlanResponse = await client.GetAsync($"/api/optimizations/{plan.Id}");
        Assert.Equal(HttpStatusCode.OK, getPlanResponse.StatusCode);
        var fetchedPlan = await getPlanResponse.Content.ReadFromJsonAsync<OptimizationPlanModel>();
        Assert.NotNull(fetchedPlan);
        Assert.Equal(plan.Id, fetchedPlan.Id);

        // 4. Apply decisions
        var safeSuggestion = plan.Suggestions.FirstOrDefault(s => s.Level == OptimizationSafetyLevel.Safe);
        var needsConfirmation = plan.Suggestions.FirstOrDefault(s => s.Level == OptimizationSafetyLevel.NeedsConfirmation);
        var forbidden = plan.Suggestions.FirstOrDefault(s => s.Level == OptimizationSafetyLevel.Forbidden);

        var decisions = new List<OptimizationDecisionModel>();
        if (safeSuggestion != null)
            decisions.Add(new(safeSuggestion.Id, Accepted: true));
        if (needsConfirmation != null)
            decisions.Add(new(needsConfirmation.Id, Accepted: true, Confirmed: true));
        if (forbidden != null)
            decisions.Add(new(forbidden.Id, Accepted: true, Confirmed: true));

        var applyRequest = new ApplyOptimizationRequestModel(plan.Version, decisions);
        var applyResponse = await client.PostAsJsonAsync($"/api/optimizations/{plan.Id}/apply", applyRequest);
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);

        var result = await applyResponse.Content.ReadFromJsonAsync<OptimizationResultModel>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AdaptedText));
        Assert.NotEmpty(result.AppliedChanges);

        // Forbidden must never be applied
        if (forbidden != null)
        {
            Assert.DoesNotContain(result.AppliedChanges, c => c.SuggestionId == forbidden.Id);
        }

        // 5. Export to PDF and DOCX
        var exportPdfResponse = await client.GetAsync($"/api/optimizations/{plan.Id}/export?format=pdf");
        Assert.Equal(HttpStatusCode.OK, exportPdfResponse.StatusCode);
        Assert.Equal("application/pdf", exportPdfResponse.Content.Headers.ContentType?.MediaType);
        var pdfBytes = await exportPdfResponse.Content.ReadAsByteArrayAsync();
        Assert.True(pdfBytes.Length > 0);

        var exportDocxResponse = await client.GetAsync($"/api/optimizations/{plan.Id}/export?format=docx");
        Assert.Equal(HttpStatusCode.OK, exportDocxResponse.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", exportDocxResponse.Content.Headers.ContentType?.MediaType);
        var docxBytes = await exportDocxResponse.Content.ReadAsByteArrayAsync();
        Assert.True(docxBytes.Length > 0);

        // 6. Export with unsupported format returns 400
        var badFormatResponse = await client.GetAsync($"/api/optimizations/{plan.Id}/export?format=txt");
        Assert.Equal(HttpStatusCode.BadRequest, badFormatResponse.StatusCode);

        // 7. Applying again with outdated version / conflict returns 409 Conflict
        var conflictRequest = new ApplyOptimizationRequestModel(plan.Version, [new(Guid.NewGuid(), Accepted: false)]);
        var conflictResponse = await client.PostAsJsonAsync($"/api/optimizations/{plan.Id}/apply", conflictRequest);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task Export_Before_Applying_Returns_Conflict()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var resume = await SeedResumeAsync(factory.Services);
        var compareResponse = await client.PostAsJsonAsync("/api/analysis/compare", new
        {
            resumeId = resume.Id,
            jobDescription = "Vaga C# Backend."
        });
        var analysis = await compareResponse.Content.ReadFromJsonAsync<AnalysisResultModel>();
        Assert.NotNull(analysis);

        var planResponse = await client.PostAsync($"/api/analysis/{analysis.Id}/optimization", null);
        var plan = await planResponse.Content.ReadFromJsonAsync<OptimizationPlanModel>();
        Assert.NotNull(plan);

        var exportResponse = await client.GetAsync($"/api/optimizations/{plan.Id}/export?format=pdf");
        Assert.Equal(HttpStatusCode.Conflict, exportResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteResume_Cascades_And_Removes_Optimizations()
    {
        await using var factory = new ResumeMatcherApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        var resume = await SeedResumeAsync(factory.Services);
        var compareResponse = await client.PostAsJsonAsync("/api/analysis/compare", new
        {
            resumeId = resume.Id,
            jobDescription = "Vaga C# Backend."
        });
        var analysis = await compareResponse.Content.ReadFromJsonAsync<AnalysisResultModel>();
        Assert.NotNull(analysis);

        var planResponse = await client.PostAsync($"/api/analysis/{analysis.Id}/optimization", null);
        var plan = await planResponse.Content.ReadFromJsonAsync<OptimizationPlanModel>();
        Assert.NotNull(plan);

        // Verify exists in db
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
            Assert.Equal(1, await db.Optimizations.CountAsync());
        }

        // Delete resume
        var deleteResponse = await client.DeleteAsync($"/api/resumes/{resume.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Optimization must be gone
        var getPlanResponse = await client.GetAsync($"/api/optimizations/{plan.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getPlanResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
            Assert.Equal(0, await db.Optimizations.CountAsync());
        }
    }

    private static async Task<ResumeEntity> SeedResumeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ResumeMatcherDbContext>();
        var resume = new ResumeEntity
        {
            OwnerUserId = "synthetic-owner-uid",
            FileName = "integration-test-opt.pdf",
            ContentType = "application/pdf",
            ExtractedText = "Desenvolvedor backend com sólida vivência em C# e APIs RESTful."
        };
        db.Resumes.Add(resume);
        await db.SaveChangesAsync();
        return resume;
    }
}
