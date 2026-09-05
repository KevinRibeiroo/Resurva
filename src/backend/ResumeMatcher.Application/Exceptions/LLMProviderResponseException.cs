namespace ResumeMatcher.Application;

public sealed class LLMProviderResponseException(string message, Exception? innerException = null)
    : Exception(message, innerException);
