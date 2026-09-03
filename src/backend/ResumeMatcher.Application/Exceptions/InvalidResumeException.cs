namespace ResumeMatcher.Application;

public sealed class InvalidResumeException(string message) : Exception(message);
