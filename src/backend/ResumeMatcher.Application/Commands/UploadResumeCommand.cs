namespace ResumeMatcher.Application;

public sealed record UploadResumeCommand(string FileName, string ContentType, Stream Content);
