namespace ResumeMatcher.Api.Models;

public sealed record CompareRequestModel(Guid ResumeId, string JobDescription);
