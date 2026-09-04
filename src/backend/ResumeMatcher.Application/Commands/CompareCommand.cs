namespace ResumeMatcher.Application;

public sealed record CompareCommand(Guid ResumeId, string JobDescription);
