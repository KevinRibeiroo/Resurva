using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;
using ResumeMatcher.Infrastructure;

namespace ResumeMatcher.Tests;

public sealed class ResumeMatcherApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"resume-matcher-tests-{Guid.NewGuid():N}";
    private readonly CountingLLMProvider _llmProvider = new();

    public int LlmCallCount => _llmProvider.CallCount;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ResumeMatcherDbContext>>();
            services.RemoveAll<ResumeMatcherDbContext>();
            services.AddDbContext<ResumeMatcherDbContext>(options => options.UseInMemoryDatabase(_databaseName));

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
