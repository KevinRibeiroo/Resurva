namespace ResumeMatcher.Application;

public sealed class ResourceNotFoundException(string message) : Exception(message);
