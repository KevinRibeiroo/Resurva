using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResumeMatcher.Api;
using ResumeMatcher.Application;

namespace ResumeMatcher.Api.Controllers;

[ApiController]
[Route("api/resumes")]
[EnableRateLimiting(ApiRateLimitOptions.PolicyName)]
public sealed class ResumesController(IResumeService service) : ControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<UploadResumeResultModel>> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            throw new InvalidResumeException("The uploaded file is empty.");
        await using var stream = file.OpenReadStream();
        var result = await service.UploadAsync(new(file.FileName, file.ContentType, stream), cancellationToken);
        return CreatedAtAction(nameof(Upload), new
        {
            id = result.Id
        }, result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
    }
}
