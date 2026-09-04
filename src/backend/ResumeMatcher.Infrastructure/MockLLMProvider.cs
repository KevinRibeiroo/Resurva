using System.Text.RegularExpressions;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Infrastructure;

public sealed partial class MockLLMProvider : ILLMProvider
{
    private static readonly string[] Skills =
    [
        "C#", ".NET", "ASP.NET Core", "Java", "Python", "JavaScript", "TypeScript", "React", "Angular", "Vue",
        "SQL", "PostgreSQL", "SQLite", "SQL Server", "MongoDB", "AWS", "Azure", "Docker", "Kubernetes",
        "Git", "REST", "GraphQL", "Redis", "RabbitMQ", "Kafka", "Entity Framework", "Scrum"
    ];

    public Task<StructuredComparisonModel> CompareAsync(string resumeText, string jobDescription, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var jobSkills = Skills.Where(skill => Contains(jobDescription, skill)).ToArray();
        var matched = jobSkills.Where(skill => Contains(resumeText, skill))
            .Select(skill => new EvidenceItemModel(skill, FindEvidence(resumeText, skill))).ToArray();
        var missing = jobSkills.Except(matched.Select(x => x.Text), StringComparer.OrdinalIgnoreCase)
            .Select(skill => new EvidenceItemModel(skill)).ToArray();

        var requirements = ExtractRequirements(jobDescription);
        var met = requirements.Where(requirement => SignificantWords(requirement).Any(word => Contains(resumeText, word)))
            .Select(requirement => new EvidenceItemModel(requirement, FindEvidence(resumeText, SignificantWords(requirement).FirstOrDefault() ?? requirement))).ToArray();
        var unmet = requirements.Except(met.Select(x => x.Text)).Select(x => new EvidenceItemModel(x)).ToArray();
        var strengths = matched.Take(4).Select(x => new EvidenceItemModel($"Experiência evidenciada em {x.Text}.", x.Evidence)).ToArray();
        var attention = missing.Take(4).Select(x => new EvidenceItemModel($"A vaga cita {x.Text}, sem evidência explícita no currículo.")).ToArray();
        var recommendations = missing.Take(3).Select(x => new EvidenceItemModel($"Não adicionar {x.Text} sem confirmação e evidência do candidato.")).ToArray();

        var reqSeniority = SeniorityEvaluator.Parse(jobDescription);
        var candSeniority = SeniorityEvaluator.Parse(resumeText);
        var seniorityMatch = SeniorityEvaluator.Evaluate(reqSeniority, candSeniority);

        var attentionList = attention.ToList();
        if (SeniorityEvaluator.IsOverqualified(reqSeniority, candSeniority))
        {
            attentionList.Add(new EvidenceItemModel(
                "O histórico profissional indica senioridade superior à exigida pela vaga, o que pode gerar possível desalinhamento de escopo, remuneração ou expectativa de carreira.",
                $"Vaga: {reqSeniority}, Candidato: {candSeniority}"));
        }

        return Task.FromResult(new StructuredComparisonModel(matched, missing, met, unmet, strengths, attentionList, recommendations,
            KeywordDimension(resumeText, jobDescription, "experiência", "anos", "desenvolvimento"),
            seniorityMatch,
            KeywordDimension(resumeText, jobDescription, "graduação", "bacharel", "tecnólogo", "formação", "superior"),
            CandidateSeniority: candSeniority == SeniorityLevel.NotSpecified ? null : candSeniority.ToString(),
            RequiredSeniority: reqSeniority == SeniorityLevel.NotSpecified ? null : reqSeniority.ToString()));
    }

    private static double KeywordDimension(string resume, string job, params string[] terms)
    {
        var required = terms.Where(term => Contains(job, term)).ToArray();
        return required.Length == 0 ? 100 : required.Count(term => Contains(resume, term)) * 100d / required.Length;
    }

    private static string[] ExtractRequirements(string text)
    {
        return [.. text.Split(['\r', '\n', ';', '•'], StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim(' ', '-', '*')).Where(x => x.Length is >= 10 and <= 240)
        .Where(x => RequirementMarker().IsMatch(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(12)];
    }

    private static IEnumerable<string> SignificantWords(string text)
    {
        return Word().Matches(text).Select(x => x.Value)
        .Where(x => x.Length >= 5).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool Contains(string text, string term)
    {
        return Regex.IsMatch(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(term)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase);
    }

    private static string? FindEvidence(string text, string term)
    {
        return text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim()).FirstOrDefault(line => Contains(line, term));
    }

    [GeneratedRegex(@"\b(requisito|obrigat[oó]rio|necess[aá]rio|experi[eê]ncia|conhecimento|forma[cç][aã]o|dom[ií]nio)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RequirementMarker();
    [GeneratedRegex(@"[\p{L}\p{N}+#.]+")]
    private static partial Regex Word();
}
