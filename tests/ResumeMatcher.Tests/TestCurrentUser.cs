using ResumeMatcher.Application;

namespace ResumeMatcher.Tests;

internal sealed class TestCurrentUser(string userId = "synthetic-owner-uid") : ICurrentUser
{
    public string UserId { get; } = userId;
}
