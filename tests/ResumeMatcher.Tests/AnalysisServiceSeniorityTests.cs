using Microsoft.Extensions.Options;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;
using Xunit;

namespace ResumeMatcher.Tests;

public sealed class AnalysisServiceSeniorityTests
{
    private sealed class FakeResumeRepository(ResumeEntity resume) : IResumeRepository
    {
        public Task<ResumeEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult<ResumeEntity?>(resume.Id == id ? resume : null);
        }

        public Task AddAsync(ResumeEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(resume.Id == id);
        }
    }

    private sealed class FakeAnalysisRepository : IAnalysisRepository
    {
        public AnalysisEntity? SavedEntity { get; private set; }

        public Task<bool> TryAddAsync(AnalysisEntity entity, CancellationToken cancellationToken)
        {
            SavedEntity = entity;
            return Task.FromResult(true);
        }

        public Task<AnalysisEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(SavedEntity);
        }

        public Task<AnalysisEntity?> GetByInputHashAsync(string analysisInputHash, CancellationToken cancellationToken)
        {
            return Task.FromResult(SavedEntity?.AnalysisInputHash == analysisInputHash ? SavedEntity : null);
        }
    }

    private sealed class FakeLLMProvider(StructuredComparisonModel comparison) : ILLMProvider
    {
        public string ModelName => "test-model";
        public string ConfigurationFingerprint => "default";
        public string PromptVersion => "v1";

        public Task<StructuredComparisonModel> CompareAsync(string resumeText, string jobDescription, CancellationToken cancellationToken)
        {
            return Task.FromResult(comparison);
        }
    }

    [Fact]
    public async Task CompareAsync_SeniorCandidateForJuniorJob_Scores100ForSeniorityAndAddsAttentionPoint()
    {
        var resumeId = Guid.NewGuid();
        var resume = new ResumeEntity
        {
            Id = resumeId,
            FileName = "resume.pdf",
            ContentType = "application/pdf",
            ExtractedText = "Desenvolvedor Backend Sênior com 8 anos de experiência em C# e .NET."
        };

        var jobDescription = "Vaga para Desenvolvedor Júnior com conhecimentos em C# e .NET.";

        var mockComparison = new StructuredComparisonModel(
            MatchedSkills: [new EvidenceItemModel("C#", "C#"), new EvidenceItemModel(".NET", ".NET")],
            MissingSkills: [],
            RequirementsMet: [new EvidenceItemModel("Conhecimento C#")],
            RequirementsMissing: [],
            Strengths: [new EvidenceItemModel("Experiência sólida")],
            PointsOfAttention: [],
            Recommendations: [],
            ExperienceMatch: 100.0,
            SeniorityMatch: 40.0, // Old LLM score that used to penalize
            EducationMatch: 100.0,
            CandidateSeniority: "Senior",
            RequiredSeniority: "Junior",
            CandidateExperienceYears: 8.0,
            RequiredExperienceYears: 1.0);

        var scoringOptions = Options.Create(new ScoringOptions
        {
            SkillsWeight = 0.40,
            ExperienceWeight = 0.30,
            SeniorityWeight = 0.15,
            RequirementsWeight = 0.10,
            EducationWeight = 0.05
        });

        var scoringEngine = new WeightedScoringEngine(scoringOptions);
        var resumeRepo = new FakeResumeRepository(resume);
        var analysisRepo = new FakeAnalysisRepository();
        var llmProvider = new FakeLLMProvider(mockComparison);

        var service = new AnalysisService(resumeRepo, analysisRepo, llmProvider, scoringEngine, scoringOptions);

        var result = await service.CompareAsync(new CompareCommand(resumeId, jobDescription), CancellationToken.None);

        // Seniority should NOT be 40, it must be 100 because candidate is overqualified/meets minimum requirement
        Assert.Equal(100.0, result.SeniorityScore);
        Assert.Equal(100.0, result.OverallScore);

        // Overqualification should be recorded in PointsOfAttention
        Assert.Contains(result.PointsOfAttention, p =>
            p.Text.Contains("senioridade superior", StringComparison.OrdinalIgnoreCase) ||
            p.Text.Contains("sobrequalifica", StringComparison.OrdinalIgnoreCase));
    }
}
