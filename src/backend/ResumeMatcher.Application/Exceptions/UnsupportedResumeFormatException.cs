namespace ResumeMatcher.Application;

public sealed class UnsupportedResumeFormatException(string message) : Exception(message);
