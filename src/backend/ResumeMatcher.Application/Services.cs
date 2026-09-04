using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Application;

public sealed class WeightedScoringEngine : IScoringEngine
{
    private readonly ScoringOptions _options;

    public WeightedScoringEngine(IOptions<ScoringOptions> options)
    {
        _options = options.Value;
        var total = _options.SkillsWeight + _options.ExperienceWeight + _options.SeniorityWeight
            + _options.RequirementsWeight + _options.EducationWeight;
        if (Math.Abs(total - 1) > 0.0001)
            throw new InvalidOperationException("Scoring weights must add up to 1.");
    }

    public ScoreBreakdownModel Calculate(ScoreComponentsModel components)
    {
        static double Normalize(double value)
        {
            return Math.Round(Math.Clamp(value, 0, 100), 2);
        }

        var skills = Normalize(components.Skills);
        var experience = Normalize(components.Experience);
        var seniority = Normalize(components.Seniority);
        var requirements = Normalize(components.Requirements);
        var education = Normalize(components.Education);
        var overall = skills * _options.SkillsWeight + experience * _options.ExperienceWeight
            + seniority * _options.SeniorityWeight + requirements * _options.RequirementsWeight
            + education * _options.EducationWeight;
        return new(Normalize(overall), skills, experience, seniority, requirements, education);
    }
}

public sealed class ResumeService(IEnumerable<IResumeTextExtractor> extractors, IResumeRepository repository) : IResumeService
{
    private const long MaxFileSize = 10 * 1024 * 1024;

    public async Task<UploadResumeResultModel> UploadAsync(UploadResumeCommand command, CancellationToken cancellationToken)
    {
        if (!command.Content.CanRead)
            throw new InvalidResumeException("The uploaded file cannot be read.");
        if (command.Content.CanSeek && command.Content.Length > MaxFileSize)
            throw new InvalidResumeException("The file exceeds the 10 MB limit.");

        var extension = Path.GetExtension(command.FileName).ToLowerInvariant();
        var extractor = extractors.FirstOrDefault(x => x.CanExtract(extension, command.ContentType))
            ?? throw new UnsupportedResumeFormatException("Only PDF and DOCX files are supported.");
        var text = (await extractor.ExtractAsync(command.Content, cancellationToken)).Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidResumeException("No text could be extracted from the resume.");

        var resume = new ResumeEntity { FileName = Path.GetFileName(command.FileName), ContentType = command.ContentType, ExtractedText = text };
        await repository.AddAsync(resume, cancellationToken);
        return new(resume.Id, resume.FileName, text.Length);
    }
}

public sealed class AnalysisService(
    IResumeRepository resumes,
    IAnalysisRepository analyses,
    ILLMProvider llmProvider,
    IScoringEngine scoringEngine,
    IOptions<ScoringOptions>? scoringOptions = null,
    ILogger<AnalysisService>? logger = null) : IAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, Lazy<Task<AnalysisResultModel>>> InFlightAnalyses = new(StringComparer.Ordinal);
    private readonly ILogger<AnalysisService> _logger = logger ?? NullLogger<AnalysisService>.Instance;

    public async Task<AnalysisResultModel> CompareAsync(CompareCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.JobDescription))
            throw new ArgumentException("Job description is required.");

        var resume = await resumes.GetAsync(command.ResumeId, cancellationToken)
            ?? throw new ResourceNotFoundException("Resume not found.");

        var analysisInputHash = AnalysisInputHasher.Generate(
            resume.ExtractedText,
            command.JobDescription,
            llmProvider.ModelName,
            llmProvider.PromptVersion,
            AnalysisInputHasher.CurrentAnalysisRulesVersion);

        var cachedResult = await GetCachedResultAsync(analysisInputHash, cancellationToken);
        if (cachedResult is not null)
        {
            _logger.LogInformation("Analysis cache hit for hash {Hash}", analysisInputHash);
            return cachedResult;
        }

        _logger.LogInformation("Analysis cache miss for hash {Hash}", analysisInputHash);

        var inFlightAnalysis = InFlightAnalyses.GetOrAdd(
            analysisInputHash,
            _ => new Lazy<Task<AnalysisResultModel>>(
                () => CreateOrGetAnalysisAsync(resume, command.JobDescription, analysisInputHash, cancellationToken),
                LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await inFlightAnalysis.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            if (inFlightAnalysis.IsValueCreated && inFlightAnalysis.Value.IsCompleted)
                InFlightAnalyses.TryRemove(analysisInputHash, out _);
        }
    }

    private async Task<AnalysisResultModel> CreateOrGetAnalysisAsync(
        ResumeEntity resume,
        string jobDescription,
        string analysisInputHash,
        CancellationToken cancellationToken)
    {
        var cachedResult = await GetCachedResultAsync(analysisInputHash, cancellationToken);
        if (cachedResult is not null)
        {
            _logger.LogInformation("Analysis cache hit for hash {Hash} after waiting for an in-flight analysis", analysisInputHash);
            return cachedResult;
        }

        _logger.LogInformation("Calling LLM for new analysis with hash {Hash}", analysisInputHash);
        var comparison = await llmProvider.CompareAsync(resume.ExtractedText, jobDescription, cancellationToken);
        var skills = Ratio(comparison.MatchedSkills.Count, comparison.MissingSkills.Count);
        var requirements = Ratio(comparison.RequirementsMet.Count, comparison.RequirementsMissing.Count);

        var reqSeniority = SeniorityEvaluator.Parse(comparison.RequiredSeniority);
        if (reqSeniority == SeniorityLevel.NotSpecified)
            reqSeniority = SeniorityEvaluator.Parse(jobDescription);

        var candSeniority = SeniorityEvaluator.Parse(comparison.CandidateSeniority);
        if (candSeniority == SeniorityLevel.NotSpecified)
            candSeniority = SeniorityEvaluator.Parse(resume.ExtractedText);

        double seniority;
        if (reqSeniority != SeniorityLevel.NotSpecified || candSeniority != SeniorityLevel.NotSpecified)
        {
            seniority = SeniorityEvaluator.Evaluate(reqSeniority, candSeniority, scoringOptions?.Value);
        }
        else
        {
            seniority = comparison.SeniorityMatch;
        }

        double experience;
        if (comparison.RequiredExperienceYears.HasValue || comparison.CandidateExperienceYears.HasValue)
        {
            experience = ExperienceEvaluator.Evaluate(comparison.RequiredExperienceYears, comparison.CandidateExperienceYears);
        }
        else
        {
            experience = comparison.ExperienceMatch;
        }

        var pointsOfAttention = comparison.PointsOfAttention.ToList();
        if (SeniorityEvaluator.IsOverqualified(reqSeniority, candSeniority) &&
            !pointsOfAttention.Any(p => p.Text.Contains("senioridade superior", StringComparison.OrdinalIgnoreCase) || p.Text.Contains("sobrequalifica", StringComparison.OrdinalIgnoreCase)))
        {
            pointsOfAttention.Add(new EvidenceItemModel(
                "O histórico profissional indica senioridade superior à exigida pela vaga, o que pode gerar possível desalinhamento de escopo, remuneração ou expectativa de carreira.",
                $"Nível exigido: {reqSeniority}, Nível identificado no candidato: {candSeniority}"));
        }

        var score = scoringEngine.Calculate(new(skills, experience, seniority, requirements, comparison.EducationMatch));
        var id = Guid.NewGuid();
        var result = new AnalysisResultModel(id, resume.Id, score.Overall, score.Skills, score.Experience, score.Seniority,
            score.Requirements, score.Education, comparison.MatchedSkills, comparison.MissingSkills,
            comparison.RequirementsMet, comparison.RequirementsMissing, comparison.Strengths,
            pointsOfAttention, comparison.Recommendations);
        var analysis = new AnalysisEntity
        {
            Id = id,
            ResumeId = resume.Id,
            AnalysisInputHash = analysisInputHash,
            JobDescription = jobDescription,
            OverallScore = score.Overall,
            SkillsScore = score.Skills,
            ExperienceScore = score.Experience,
            SeniorityScore = score.Seniority,
            RequirementsScore = score.Requirements,
            EducationScore = score.Education,
            ResultJson = JsonSerializer.Serialize(result, JsonOptions)
        };

        if (!await analyses.TryAddAsync(analysis, cancellationToken))
        {
            var concurrentlyPersistedResult = await GetCachedResultAsync(analysisInputHash, cancellationToken)
                ?? throw new InvalidOperationException("An analysis input hash conflict occurred, but the persisted analysis could not be loaded.");
            _logger.LogInformation("Analysis cache hit for hash {Hash} after a concurrent insert", analysisInputHash);
            return concurrentlyPersistedResult;
        }

        _logger.LogInformation("Persisted new analysis with hash {Hash}", analysisInputHash);
        return result;
    }

    private async Task<AnalysisResultModel?> GetCachedResultAsync(string analysisInputHash, CancellationToken cancellationToken)
    {
        var analysis = await analyses.GetByInputHashAsync(analysisInputHash, cancellationToken);
        if (analysis is null)
            return null;

        return JsonSerializer.Deserialize<AnalysisResultModel>(analysis.ResultJson, JsonOptions)
            ?? throw new InvalidOperationException("The persisted analysis result could not be deserialized.");
    }

    public async Task<AnalysisResultModel?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var analysis = await analyses.GetAsync(id, cancellationToken);
        return analysis is null ? null : JsonSerializer.Deserialize<AnalysisResultModel>(analysis.ResultJson, JsonOptions);
    }

    private static double Ratio(int matched, int missing)
    {
        return matched + missing == 0 ? 0 : matched * 100d / (matched + missing);
    }
}
