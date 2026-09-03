using System.Text.Json;
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

    public ScoreBreakdown Calculate(ScoreComponents components)
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

    public async Task<UploadResumeResult> UploadAsync(UploadResumeCommand command, CancellationToken cancellationToken)
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

        var resume = new Resume { FileName = Path.GetFileName(command.FileName), ContentType = command.ContentType, ExtractedText = text };
        await repository.AddAsync(resume, cancellationToken);
        return new(resume.Id, resume.FileName, text.Length);
    }
}

public sealed class AnalysisService(
    IResumeRepository resumes,
    IAnalysisRepository analyses,
    ILLMProvider llmProvider,
    IScoringEngine scoringEngine) : IAnalysisService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AnalysisResult> CompareAsync(CompareCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.JobDescription))
            throw new ArgumentException("Job description is required.");
        var resume = await resumes.GetAsync(command.ResumeId, cancellationToken)
            ?? throw new ResourceNotFoundException("Resume not found.");
        var comparison = await llmProvider.CompareAsync(resume.ExtractedText, command.JobDescription, cancellationToken);
        var skills = Ratio(comparison.MatchedSkills.Count, comparison.MissingSkills.Count);
        var requirements = Ratio(comparison.RequirementsMet.Count, comparison.RequirementsMissing.Count);
        var score = scoringEngine.Calculate(new(skills, comparison.ExperienceMatch, comparison.SeniorityMatch, requirements, comparison.EducationMatch));
        var id = Guid.NewGuid();
        var result = new AnalysisResult(id, resume.Id, score.Overall, score.Skills, score.Experience, score.Seniority,
            score.Requirements, score.Education, comparison.MatchedSkills, comparison.MissingSkills,
            comparison.RequirementsMet, comparison.RequirementsMissing, comparison.Strengths,
            comparison.PointsOfAttention, comparison.Recommendations);
        var analysis = new Analysis
        {
            Id = id,
            ResumeId = resume.Id,
            JobDescription = command.JobDescription,
            OverallScore = score.Overall,
            SkillsScore = score.Skills,
            ExperienceScore = score.Experience,
            SeniorityScore = score.Seniority,
            RequirementsScore = score.Requirements,
            EducationScore = score.Education,
            ResultJson = JsonSerializer.Serialize(result, JsonOptions)
        };
        await analyses.AddAsync(analysis, cancellationToken);
        return result;
    }

    public async Task<AnalysisResult?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var analysis = await analyses.GetAsync(id, cancellationToken);
        return analysis is null ? null : JsonSerializer.Deserialize<AnalysisResult>(analysis.ResultJson, JsonOptions);
    }

    private static double Ratio(int matched, int missing)
    {
        return matched + missing == 0 ? 0 : matched * 100d / (matched + missing);
    }
}
