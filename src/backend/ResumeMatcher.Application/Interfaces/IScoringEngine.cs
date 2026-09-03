namespace ResumeMatcher.Application;

public interface IScoringEngine
{
    ScoreBreakdownModel Calculate(ScoreComponentsModel components);
}
