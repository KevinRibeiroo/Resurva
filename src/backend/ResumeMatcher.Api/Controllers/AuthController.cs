using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace ResumeMatcher.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
public sealed class AuthController(ILogger<AuthController> logger) : ControllerBase
{
    [HttpGet("session")]
    public IActionResult Session()
    {
        var email = User.FindFirst("email")?.Value;
        logger.LogInformation("Sessão ativa verificada para o usuário autenticado '{Email}'", email);
        return NoContent();
    }
}
