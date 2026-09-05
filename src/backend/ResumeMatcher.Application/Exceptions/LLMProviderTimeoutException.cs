namespace ResumeMatcher.Application;

public sealed class LLMProviderTimeoutException(string message, Exception? innerException = null)
    : Exception(message, innerException);
