using System.Collections.Concurrent;
using System.Text.Json;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Tests;

public sealed class AnalysisCacheTests
{
    [Fact]
    public async Task SecondIdenticalComparisonReturnsPersistedResultWithoutCallingLlmAgain()
    {
        var resume = CreateResume();
        var analysisRepository = new FakeAnalysisRepository();
        var llmProvider = new CountingLLMProvider();
        var service = CreateService(resume, analysisRepository, llmProvider);
        var command = new CompareCommand(resume.Id, "Vaga backend com C# e .NET.");

        var first = await service.CompareAsync(command, CancellationToken.None);
        var second = await service.CompareAsync(command, CancellationToken.None);

        Assert.Equal(1, llmProvider.CallCount);
        Assert.Equal(1, analysisRepository.Count);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.OverallScore, second.OverallScore);
        Assert.Equal(first.SeniorityScore, second.SeniorityScore);

        var persisted = JsonSerializer.Deserialize<AnalysisResultModel>(
            analysisRepository.Single.ResultJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(persisted);
        Assert.Equal(persisted.OverallScore, second.OverallScore);
        Assert.Equal(persisted.SkillsScore, second.SkillsScore);
        Assert.Equal(persisted.ExperienceScore, second.ExperienceScore);
        Assert.Equal(persisted.SeniorityScore, second.SeniorityScore);
        Assert.Equal(persisted.RequirementsScore, second.RequirementsScore);
        Assert.Equal(persisted.EducationScore, second.EducationScore);
    }

    [Fact]
    public async Task ConcurrentIdenticalComparisonsShareOneLlmCall()
    {
        var resume = CreateResume();
        var analysisRepository = new FakeAnalysisRepository();
        var llmProvider = new CountingLLMProvider(delay: TimeSpan.FromMilliseconds(100));
        var firstService = CreateService(resume, analysisRepository, llmProvider);
        var secondService = CreateService(resume, analysisRepository, llmProvider);
        var command = new CompareCommand(resume.Id, "Vaga concorrente para backend C#.");

        var results = await Task.WhenAll(
            firstService.CompareAsync(command, CancellationToken.None),
            secondService.CompareAsync(command, CancellationToken.None));

        Assert.Equal(1, llmProvider.CallCount);
        Assert.Equal(1, analysisRepository.Count);
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(results[0].OverallScore, results[1].OverallScore);
    }

    private static AnalysisService CreateService(
        ResumeEntity resume,
        FakeAnalysisRepository analysisRepository,
        CountingLLMProvider llmProvider)
    {
        return new AnalysisService(
            new FakeResumeRepository(resume),
            analysisRepository,
            llmProvider,
            new FixedScoringEngine());
    }

    private static ResumeEntity CreateResume()
    {
        return new ResumeEntity
        {
            Id = Guid.NewGuid(),
            FileName = "resume.pdf",
            ContentType = "application/pdf",
            ExtractedText = "Desenvolvedor backend com experiência em C# e .NET."
        };
    }

    private sealed class FakeResumeRepository(ResumeEntity resume) : IResumeRepository
    {
        public Task AddAsync(ResumeEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ResumeEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult<ResumeEntity?>(resume.Id == id ? resume : null);
        }
    }

    private sealed class FakeAnalysisRepository : IAnalysisRepository
    {
        private readonly ConcurrentDictionary<string, AnalysisEntity> _analyses = new(StringComparer.Ordinal);

        public int Count => _analyses.Count;
        public AnalysisEntity Single => _analyses.Values.Single();

        public Task<bool> TryAddAsync(AnalysisEntity analysis, CancellationToken cancellationToken)
        {
            return Task.FromResult(_analyses.TryAdd(analysis.AnalysisInputHash!, analysis));
        }

        public Task<AnalysisEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(_analyses.Values.SingleOrDefault(x => x.Id == id));
        }

        public Task<AnalysisEntity?> GetByInputHashAsync(string analysisInputHash, CancellationToken cancellationToken)
        {
            _analyses.TryGetValue(analysisInputHash, out var analysis);
            return Task.FromResult(analysis);
        }
    }

    private sealed class CountingLLMProvider(TimeSpan? delay = null) : ILLMProvider
    {
        private int _callCount;

        public int CallCount => _callCount;
        public string ModelName => "test-model";
        public string PromptVersion => "v1";

        public async Task<StructuredComparisonModel> CompareAsync(
            string resumeText,
            string jobDescription,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            if (delay.HasValue)
                await Task.Delay(delay.Value, cancellationToken);

            return new StructuredComparisonModel(
                MatchedSkills: [new EvidenceItemModel("C#", "experiência em C#")],
                MissingSkills: [],
                RequirementsMet: [new EvidenceItemModel("Backend", "Desenvolvedor backend")],
                RequirementsMissing: [],
                Strengths: [new EvidenceItemModel("Experiência backend")],
                PointsOfAttention: [],
                Recommendations: [],
                ExperienceMatch: 90,
                SeniorityMatch: 80,
                EducationMatch: 70);
        }
    }

    private sealed class FixedScoringEngine : IScoringEngine
    {
        public ScoreBreakdownModel Calculate(ScoreComponentsModel components)
        {
            return new ScoreBreakdownModel(86, 100, 90, 80, 100, 70);
        }
    }
}
