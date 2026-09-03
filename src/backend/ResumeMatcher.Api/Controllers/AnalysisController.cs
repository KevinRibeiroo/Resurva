using Microsoft.AspNetCore.Mvc;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Api.Controllers;

public sealed record CompareRequest(Guid ResumeId, string JobDescription);

[ApiController]
[Route("api/analysis")]
public sealed class AnalysisController(IAnalysisService service) : ControllerBase
{
    [HttpPost("compare")]
    public async Task<ActionResult<AnalysisResult>> Compare(CompareRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CompareAsync(new(request.ResumeId, request.JobDescription), cancellationToken);
        return CreatedAtAction(nameof(Get), new
        {
            id = result.Id
        }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnalysisResult>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
