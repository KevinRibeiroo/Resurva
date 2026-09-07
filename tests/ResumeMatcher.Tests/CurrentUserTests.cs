using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ResumeMatcher.Api;

namespace ResumeMatcher.Tests;

public sealed class CurrentUserTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", true)]
    [InlineData("untrusted-subject", false)]
    public void MissingOrUnauthenticatedSubjectFailsClosed(string? subject, bool authenticated)
    {
        var identity = new ClaimsIdentity(subject is null ? [] : [new Claim("sub", subject)], authenticated ? "test" : null);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        Assert.Throws<UnauthorizedAccessException>(() => new CurrentUser(accessor).UserId);
    }

    [Fact]
    public void UsesVerifiedPrincipalSubjectRatherThanEmailOrRequestData()
    {
        var identity = new ClaimsIdentity([new Claim("sub", "test-uid"), new Claim("email", "synthetic@example.test")], "test");
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } };
        Assert.Equal("test-uid", new CurrentUser(accessor).UserId);
    }
}
