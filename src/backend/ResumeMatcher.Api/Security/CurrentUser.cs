using ResumeMatcher.Application;

namespace ResumeMatcher.Api;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            var id = principal?.FindFirst("sub")?.Value;
            if (principal?.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(id) || id.Length > 128)
                throw new UnauthorizedAccessException("An authenticated user is required.");
            return id;
        }
    }
}
