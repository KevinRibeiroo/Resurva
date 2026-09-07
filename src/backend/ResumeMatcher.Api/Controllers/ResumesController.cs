using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using ResumeMatcher.Api;
using ResumeMatcher.Application;

namespace ResumeMatcher.Api.Controllers;

[ApiController]
[Route("api/resumes")]
[EnableRateLimiting(ApiRateLimitOptions.PolicyName)]
public sealed class ResumesController(
    IResumeService service,
    ILogger<ResumesController> logger) : ControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<UploadResumeResultModel>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            throw new InvalidResumeException("The uploaded file is empty.");

        logger.LogInformation("Recebido arquivo de currículo ({Length} bytes)", file.Length);

        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(new(file.FileName, file.ContentType, stream), cancellationToken);

        logger.LogInformation("Currículo processado e persistido com ID {ResumeId}", result.Id);

        return CreatedAtAction(nameof(Upload), new
        {
            id = result.Id
        }, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Recebida requisição para excluir currículo ID {ResumeId}", id);
        var deleted = await service.DeleteAsync(id, cancellationToken);
        if (deleted)
        {
            logger.LogInformation("Currículo ID {ResumeId} e análises associadas foram excluídos com sucesso", id);
            return NoContent();
        }

        logger.LogWarning("Currículo ID {ResumeId} não encontrado para exclusão", id);
        return NotFound();
    }
}
