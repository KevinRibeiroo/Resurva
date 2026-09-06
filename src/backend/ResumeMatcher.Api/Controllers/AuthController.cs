using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ResumeMatcher.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    [HttpGet("session")]
    public IActionResult Session() => NoContent();
}
