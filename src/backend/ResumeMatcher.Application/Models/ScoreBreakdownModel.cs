namespace ResumeMatcher.Application;

public sealed record ScoreBreakdownModel(
    double Overall,
    double Skills,
    double Experience,
    double Seniority,
    double Requirements,
    double Education);
