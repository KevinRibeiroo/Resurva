namespace ResumeMatcher.Application;

public static class ExperienceEvaluator
{
    public static double Evaluate(double? requiredYears, double? candidateYears)
    {
        if (requiredYears is null or <= 0)
            return 100.0;

        if (candidateYears is null or < 0)
            return 0.0;

        if (candidateYears >= requiredYears)
            return 100.0;

        var ratio = (candidateYears.Value / requiredYears.Value) * 100.0;
        return Math.Round(Math.Clamp(ratio, 0.0, 100.0), 2);
    }
}
