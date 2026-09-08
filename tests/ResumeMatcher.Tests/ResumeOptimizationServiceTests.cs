using System.Text.Json;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;
using ResumeMatcher.Infrastructure;

namespace ResumeMatcher.Tests;

public sealed class ResumeOptimizationServiceTests
{
    private readonly TestCurrentUser _currentUser = new("user-test-123");
    private readonly TestTimeProvider _clock = new();
    private readonly OptimizationSafetyValidator _safetyValidator = new();

    [Fact]
    public async Task CreatePlan_Downgrades_Safe_Without_Evidence_To_NeedsConfirmation()
    {
        var resumeRepo = new InMemoryResumeRepository();
        var analysisRepo = new InMemoryAnalysisRepository();
        var optRepo = new InMemoryOptimizationRepository();

        var resume = new ResumeEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = _currentUser.UserId,
            FileName = "curriculo.pdf",
            ContentType = "application/pdf",
            ExtractedText = "Experiência de 5 anos com C# e ASP.NET Core.",
            CreatedAt = _clock.GetUtcNow(),
            UpdatedAt = _clock.GetUtcNow()
        };
        await resumeRepo.AddAsync(resume, CancellationToken.None);

        var analysisId = Guid.NewGuid();
        var analysis = new AnalysisEntity
        {
            Id = analysisId,
            OwnerUserId = _currentUser.UserId,
            ResumeId = resume.Id,
            JobDescription = "Vaga C# e PostgreSQL",
            AnalysisInputHash = "hash-123",
            ResultJson = JsonSerializer.Serialize(new AnalysisResultModel(
                analysisId, resume.Id, 80, 80, 80, 80, 80, 80,
                [], [], [], [], [], [], [])),
            CreatedAt = _clock.GetUtcNow(),
            UpdatedAt = _clock.GetUtcNow()
        };
        await analysisRepo.TryAddAsync(analysis, CancellationToken.None);

        // Provider returning a Safe suggestion without evidence
        var fakeProvider = new FakeOptimizationProvider(
        [
            new OptimizationSuggestionModel(
                Guid.NewGuid(),
                OptimizationSafetyLevel.Safe,
                false,
                "",
                "Especialista em Docker e Kubernetes",
                "Alinhamento à vaga de nuvem",
                null, // No evidence!
                null)
        ]);

        var service = new ResumeOptimizationService(
            resumeRepo, analysisRepo, optRepo, fakeProvider, _safetyValidator, _currentUser, _clock);

        var plan = await service.CreatePlanAsync(analysis.Id, CancellationToken.None);

        Assert.NotNull(plan);
        Assert.Single(plan.Suggestions);
        var suggestion = plan.Suggestions[0];
        Assert.Equal(OptimizationSafetyLevel.NeedsConfirmation, suggestion.Level);
        Assert.False(string.IsNullOrWhiteSpace(suggestion.ConfirmationQuestion));
    }

    [Fact]
    public async Task CreatePlan_Keeps_Safe_When_Evidence_Matches_ResumeText()
    {
        var resumeRepo = new InMemoryResumeRepository();
        var analysisRepo = new InMemoryAnalysisRepository();
        var optRepo = new InMemoryOptimizationRepository();

        var resume = new ResumeEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = _currentUser.UserId,
            FileName = "curriculo.pdf",
            ContentType = "application/pdf",
            ExtractedText = "Desenvolvedor Backend com sólida vivência em C# e testes automatizados.",
            CreatedAt = _clock.GetUtcNow(),
            UpdatedAt = _clock.GetUtcNow()
        };
        await resumeRepo.AddAsync(resume, CancellationToken.None);

        var analysis = new AnalysisEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = _currentUser.UserId,
            ResumeId = resume.Id,
            JobDescription = "Desenvolvedor C#",
            AnalysisInputHash = "hash-456",
            ResultJson = "{}",
            CreatedAt = _clock.GetUtcNow(),
            UpdatedAt = _clock.GetUtcNow()
        };
        await analysisRepo.TryAddAsync(analysis, CancellationToken.None);

        var fakeProvider = new FakeOptimizationProvider(
        [
            new OptimizationSuggestionModel(
                Guid.NewGuid(),
                OptimizationSafetyLevel.Safe,
                false,
                "sólida vivência em C#",
                "sólida vivência em C# com foco em arquitetura limpa e boas práticas",
                "Destacar qualidade",
                "sólida vivência em C#",
                null)
        ]);

        var service = new ResumeOptimizationService(
            resumeRepo, analysisRepo, optRepo, fakeProvider, _safetyValidator, _currentUser, _clock);

        var plan = await service.CreatePlanAsync(analysis.Id, CancellationToken.None);

        Assert.Single(plan.Suggestions);
        Assert.Equal(OptimizationSafetyLevel.Safe, plan.Suggestions[0].Level);
    }

    [Fact]
    public async Task ApplyDecisions_Enforces_Safety_And_Version_Concurrency()
    {
        var resumeRepo = new InMemoryResumeRepository();
        var analysisRepo = new InMemoryAnalysisRepository();
        var optRepo = new InMemoryOptimizationRepository();

        var resume = new ResumeEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = _currentUser.UserId,
            FileName = "curriculo.pdf",
            ContentType = "application/pdf",
            ExtractedText = "Programador pleno C#.",
            CreatedAt = _clock.GetUtcNow(),
            UpdatedAt = _clock.GetUtcNow()
        };
        await resumeRepo.AddAsync(resume, CancellationToken.None);

        var analysis = new AnalysisEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = _currentUser.UserId,
            ResumeId = resume.Id,
            JobDescription = "Vaga C#",
            AnalysisInputHash = "hash-789",
            ResultJson = "{}",
            CreatedAt = _clock.GetUtcNow(),
            UpdatedAt = _clock.GetUtcNow()
        };
        await analysisRepo.TryAddAsync(analysis, CancellationToken.None);

        var safeId = Guid.NewGuid();
        var needsConfirmId = Guid.NewGuid();
        var forbiddenId = Guid.NewGuid();

        var fakeProvider = new FakeOptimizationProvider(
        [
            new OptimizationSuggestionModel(safeId, OptimizationSafetyLevel.Safe, false, "Programador pleno C#", "Desenvolvedor Pleno C#", "Termo mais comum", "Programador pleno C#", null),
            new OptimizationSuggestionModel(needsConfirmId, OptimizationSafetyLevel.NeedsConfirmation, false, "", "Experiência com Docker", "Exigido pela vaga", null, "Você possui experiência com Docker?"),
            new OptimizationSuggestionModel(forbiddenId, OptimizationSafetyLevel.Forbidden, false, "", "Diretor de Engenharia de 100 pessoas", "Inventado", null, null)
        ]);

        var service = new ResumeOptimizationService(
            resumeRepo, analysisRepo, optRepo, fakeProvider, _safetyValidator, _currentUser, _clock);

        var plan = await service.CreatePlanAsync(analysis.Id, CancellationToken.None);

        // 1. Version conflict throws OptimizationConflictException
        await Assert.ThrowsAsync<OptimizationConflictException>(() =>
            service.ApplyDecisionsAsync(plan.Id, new ApplyOptimizationCommand(99, []), CancellationToken.None));

        // 2. Apply decisions:
        // - Safe: Accepted
        // - NeedsConfirmation: Accepted with Confirmed = true
        // - Forbidden: Accepted with Confirmed = true (MUST BE BLOCKED by safety validator)
        var decisions = new List<OptimizationDecisionModel>
        {
            new(safeId, Accepted: true),
            new(needsConfirmId, Accepted: true, Confirmed: true),
            new(forbiddenId, Accepted: true, Confirmed: true)
        };

        var result = await service.ApplyDecisionsAsync(plan.Id, new ApplyOptimizationCommand(1, decisions), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.AppliedChanges.Count); // Safe + NeedsConfirmation only, Forbidden rejected
        Assert.Contains(result.AppliedChanges, c => c.SuggestionId == safeId);
        Assert.Contains(result.AppliedChanges, c => c.SuggestionId == needsConfirmId);
        Assert.DoesNotContain(result.AppliedChanges, c => c.SuggestionId == forbiddenId);
        Assert.Contains("Desenvolvedor Pleno C#", result.AdaptedText);
        Assert.Contains("Experiência com Docker", result.AdaptedText);
        Assert.DoesNotContain("Diretor de Engenharia", result.AdaptedText);

        // 3. Idempotent replay: calling again with the exact same decisions returns the same result
        var replay = await service.ApplyDecisionsAsync(plan.Id, new ApplyOptimizationCommand(1, decisions), CancellationToken.None);
        Assert.Equal(result.AdaptedText, replay.AdaptedText);
        Assert.Equal(result.AppliedChanges.Count, replay.AppliedChanges.Count);

        // 4. Calling again with conflicting decisions throws OptimizationConflictException
        var conflictingDecisions = new List<OptimizationDecisionModel>
        {
            new(safeId, Accepted: false)
        };
        await Assert.ThrowsAsync<OptimizationConflictException>(() =>
            service.ApplyDecisionsAsync(plan.Id, new ApplyOptimizationCommand(1, conflictingDecisions), CancellationToken.None));
    }

    [Fact]
    public async Task ApplyDecisions_WithUserDeclaration_SetsOriginToDeclaradaPeloUsuarioAndAppliesText()
    {
        var resumeRepo = new InMemoryResumeRepository();
        var analysisRepo = new InMemoryAnalysisRepository();
        var optRepo = new InMemoryOptimizationRepository();

        var resume = new ResumeEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = _currentUser.UserId,
            FileName = "curriculo.pdf",
            ContentType = "application/pdf",
            ExtractedText = "Desenvolvedor Backend C# com vivência em SQL Server.",
            CreatedAt = _clock.GetUtcNow(),
            UpdatedAt = _clock.GetUtcNow()
        };
        await resumeRepo.AddAsync(resume, CancellationToken.None);

        var analysisId = Guid.NewGuid();
        var analysis = new AnalysisEntity
        {
            Id = analysisId,
            OwnerUserId = _currentUser.UserId,
            ResumeId = resume.Id,
            JobDescription = "Vaga C# e PostgreSQL",
            AnalysisInputHash = "hash-user-decl",
            ResultJson = JsonSerializer.Serialize(new AnalysisResultModel(
                analysisId, resume.Id, 80, 80, 80, 80, 80, 80,
                [], [], [], [], [], [], [])),
            CreatedAt = _clock.GetUtcNow(),
            UpdatedAt = _clock.GetUtcNow()
        };
        await analysisRepo.TryAddAsync(analysis, CancellationToken.None);

        var needsConfId = Guid.NewGuid();
        var fakeProvider = new FakeOptimizationProvider(
        [
            new OptimizationSuggestionModel(
                needsConfId,
                OptimizationSafetyLevel.NeedsConfirmation,
                false,
                "",
                "Experiência com PostgreSQL",
                "Requisito da vaga não mencionado no currículo",
                null,
                "Você possui experiência com PostgreSQL em projetos?")
        ]);

        var service = new ResumeOptimizationService(
            resumeRepo, analysisRepo, optRepo, fakeProvider, _safetyValidator, _currentUser, _clock);

        var plan = await service.CreatePlanAsync(analysis.Id, CancellationToken.None);

        // Accept and confirm with custom UserDeclaration
        const string declaration = "Atuação com PostgreSQL em microsserviços de cobrança por 2 anos.";
        var decisions = new List<OptimizationDecisionModel>
        {
            new(needsConfId, Accepted: true, Confirmed: true, UserDeclaration: declaration)
        };

        var result = await service.ApplyDecisionsAsync(plan.Id, new ApplyOptimizationCommand(1, decisions), CancellationToken.None);

        Assert.Single(result.AppliedChanges);
        var applied = result.AppliedChanges[0];
        Assert.Equal("DeclaradaPeloUsuario", applied.InformationOrigin);
        Assert.Equal(declaration, applied.UserDeclaration);
        Assert.True(applied.WasConfirmed);
        Assert.Contains(declaration, result.AdaptedText);

        // Replay with identical declaration succeeds
        var replay = await service.ApplyDecisionsAsync(plan.Id, new ApplyOptimizationCommand(1, decisions), CancellationToken.None);
        Assert.Equal(result.AdaptedText, replay.AdaptedText);

        // Replay with changed UserDeclaration throws conflict exception
        var conflictDecisions = new List<OptimizationDecisionModel>
        {
            new(needsConfId, Accepted: true, Confirmed: true, UserDeclaration: "Outro texto de declaração")
        };
        await Assert.ThrowsAsync<OptimizationConflictException>(() =>
            service.ApplyDecisionsAsync(plan.Id, new ApplyOptimizationCommand(1, conflictDecisions), CancellationToken.None));
    }

    private sealed class FakeOptimizationProvider(IReadOnlyList<OptimizationSuggestionModel> suggestions) : IResumeOptimizationProvider
    {
        public string ModelName => "fake-opt-model";
        public string ConfigurationFingerprint => "fake-opt-fingerprint";
        public string PromptVersion => "v1-opt";

        public Task<IReadOnlyList<OptimizationSuggestionModel>> GenerateSuggestionsAsync(
            string resumeText,
            string jobDescription,
            StructuredComparisonModel comparisonContext,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(suggestions);
        }
    }

    private sealed class InMemoryResumeRepository : IResumeRepository
    {
        private readonly Dictionary<Guid, ResumeEntity> _resumes = new();
        public Task<ResumeEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(_resumes.GetValueOrDefault(id));
        public Task AddAsync(ResumeEntity resume, CancellationToken cancellationToken)
        {
            _resumes[resume.Id] = resume;
            return Task.CompletedTask;
        }
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(_resumes.Remove(id));
        }
    }

    private sealed class InMemoryAnalysisRepository : IAnalysisRepository
    {
        private readonly Dictionary<Guid, AnalysisEntity> _analyses = new();
        public Task<AnalysisEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(_analyses.GetValueOrDefault(id));
        public Task<AnalysisEntity?> GetByInputHashAsync(string hash, CancellationToken cancellationToken)
            => Task.FromResult(_analyses.Values.FirstOrDefault(a => a.AnalysisInputHash == hash));
        public Task<bool> TryAddAsync(AnalysisEntity analysis, CancellationToken cancellationToken)
        {
            if (_analyses.Values.Any(a => a.AnalysisInputHash == analysis.AnalysisInputHash))
                return Task.FromResult(false);
            _analyses[analysis.Id] = analysis;
            return Task.FromResult(true);
        }
    }

    private sealed class InMemoryOptimizationRepository : IResumeOptimizationRepository
    {
        private readonly Dictionary<Guid, ResumeOptimizationEntity> _optimizations = new();
        public Task<ResumeOptimizationEntity?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(_optimizations.GetValueOrDefault(id));
        public Task AddAsync(ResumeOptimizationEntity optimization, CancellationToken cancellationToken)
        {
            _optimizations[optimization.Id] = optimization;
            return Task.CompletedTask;
        }
        public Task UpdateAsync(ResumeOptimizationEntity optimization, CancellationToken cancellationToken)
        {
            _optimizations[optimization.Id] = optimization;
            return Task.CompletedTask;
        }
        public Task DeleteByResumeIdAsync(Guid resumeId, CancellationToken cancellationToken)
        {
            var keys = _optimizations.Where(kv => kv.Value.ResumeId == resumeId).Select(kv => kv.Key).ToList();
            foreach (var key in keys) _optimizations.Remove(key);
            return Task.CompletedTask;
        }
    }
}
