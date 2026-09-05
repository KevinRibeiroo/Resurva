namespace ResumeMatcher.Application;

public sealed class LLMProviderUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
