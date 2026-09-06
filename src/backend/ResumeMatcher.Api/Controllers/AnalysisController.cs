using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using ResumeMatcher.Api;
using ResumeMatcher.Api.Models;
using ResumeMatcher.Application;
using ResumeMatcher.Domain;

namespace ResumeMatcher.Api.Controllers;

[ApiController]
[Route("api/analysis")]
[EnableRateLimiting(ApiRateLimitOptions.PolicyName)]
public sealed class AnalysisController(
    IAnalysisService service,
    ILogger<AnalysisController> logger) : ControllerBase
{
    [HttpPost("compare")]
    public async Task<ActionResult<AnalysisResultModel>> Compare(CompareRequestModel request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Recebida solicitação de comparação para ResumeId {ResumeId}. Tamanho da descrição da vaga: {Length} caracteres",
            request.ResumeId, request.JobDescription.Length);

        var result = await service.CompareAsync(new(request.ResumeId, request.JobDescription), cancellationToken);

        logger.LogInformation("Comparação concluída para ResumeId {ResumeId}. AnalysisId gerado: {AnalysisId}, OverallScore: {Score}",
            request.ResumeId, result.Id, result.OverallScore);

        return CreatedAtAction(nameof(Get), new
        {
            id = result.Id
        }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnalysisResultModel>> Get(Guid id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Consultando análise com ID {AnalysisId}", id);
        var result = await service.GetAsync(id, cancellationToken);
        if (result is null)
        {
            logger.LogWarning("Análise com ID {AnalysisId} não encontrada", id);
            return NotFound();
        }

        logger.LogInformation("Análise com ID {AnalysisId} recuperada com sucesso (ResumeId: {ResumeId})", id, result.ResumeId);
        return Ok(result);
    }
}
