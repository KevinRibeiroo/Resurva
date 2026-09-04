namespace ResumeMatcher.Application;

public static class SeniorityEvaluator
{
    public static double Evaluate(SeniorityLevel required, SeniorityLevel candidate, ScoringOptions? options = null)
    {
        options ??= new ScoringOptions();

        if (required == SeniorityLevel.NotSpecified)
            return 100.0;

        if (candidate >= required && candidate != SeniorityLevel.NotSpecified)
            return 100.0;

        return (required, candidate) switch
        {
            (SeniorityLevel.Pleno, SeniorityLevel.Junior) => options.PlenoRequiredJuniorScore,
            (SeniorityLevel.Senior, SeniorityLevel.Pleno) => options.SeniorRequiredPlenoScore,
            (SeniorityLevel.Senior, SeniorityLevel.Junior) => options.SeniorRequiredJuniorScore,
            (SeniorityLevel.Lead, SeniorityLevel.Senior) => options.SeniorRequiredPlenoScore,
            (SeniorityLevel.Lead, SeniorityLevel.Pleno) => options.SeniorRequiredJuniorScore,
            (SeniorityLevel.Lead, SeniorityLevel.Junior) => Math.Max(0.0, options.SeniorRequiredJuniorScore - 15.0),
            _ => 50.0
        };
    }

    public static bool IsOverqualified(SeniorityLevel required, SeniorityLevel candidate)
    {
        if (required == SeniorityLevel.NotSpecified || candidate == SeniorityLevel.NotSpecified)
            return false;

        return candidate > required;
    }

    public static SeniorityLevel Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return SeniorityLevel.NotSpecified;

        var normalized = text.Trim().ToLowerInvariant();

        if (normalized.Contains("lead") || normalized.Contains("lider") || normalized.Contains("líder") ||
            normalized.Contains("especialista") || normalized.Contains("principal") || normalized.Contains("staff"))
            return SeniorityLevel.Lead;

        if (normalized.Contains("senior") || normalized.Contains("sênior") || normalized.Contains("sr"))
            return SeniorityLevel.Senior;

        if (normalized.Contains("pleno") || normalized.Contains("mid") || normalized.Contains("pl"))
            return SeniorityLevel.Pleno;

        if (normalized.Contains("junior") || normalized.Contains("júnior") || normalized.Contains("jr") ||
            normalized.Contains("estagi") || normalized.Contains("trainee") || normalized.Contains("iniciante"))
            return SeniorityLevel.Junior;

        return SeniorityLevel.NotSpecified;
    }
}
