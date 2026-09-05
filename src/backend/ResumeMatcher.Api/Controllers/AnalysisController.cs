using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResumeMatcher.Api;
using ResumeMatcher.Api.Models;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Api.Controllers;

[ApiController]
[Route("api/analysis")]
[EnableRateLimiting(ApiRateLimitOptions.PolicyName)]
public sealed class AnalysisController(IAnalysisService service) : ControllerBase
{
    [HttpPost("compare")]
    public async Task<ActionResult<AnalysisResultModel>> Compare(CompareRequestModel request, CancellationToken cancellationToken)
    {
        var result = await service.CompareAsync(new(request.ResumeId, request.JobDescription), cancellationToken);
        return CreatedAtAction(nameof(Get), new
        {
            id = result.Id
        }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnalysisResultModel>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
