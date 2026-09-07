using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using ResumeMatcher.Api.Models;
using ResumeMatcher.Application;

namespace ResumeMatcher.Api.Controllers;

[ApiController]
[Route("api")]
[EnableRateLimiting(ApiRateLimitOptions.PolicyName)]
public sealed class OptimizationsController(
    IResumeOptimizationService service,
    ILogger<OptimizationsController> logger) : ControllerBase
{
    [HttpPost("analysis/{analysisId:guid}/optimization")]
    public async Task<ActionResult<OptimizationPlanModel>> CreatePlan(Guid analysisId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Recebida solicitação de plano de otimização para AnalysisId {AnalysisId}", analysisId);
        var plan = await service.CreatePlanAsync(analysisId, cancellationToken);
        logger.LogInformation("Plano de otimização {OptimizationId} gerado com sucesso com {Count} sugestões",
            plan.Id, plan.Suggestions.Count);

        return CreatedAtAction(nameof(Get), new { id = plan.Id }, plan);
    }

    [HttpGet("optimizations/{id:guid}")]
    public async Task<ActionResult<OptimizationPlanModel>> Get(Guid id, CancellationToken cancellationToken)
    {
        logger.LogInformation("Consultando plano de otimização {OptimizationId}", id);
        var plan = await service.GetPlanAsync(id, cancellationToken);
        if (plan is null)
        {
            logger.LogWarning("Plano de otimização {OptimizationId} não encontrado", id);
            return NotFound();
        }

        return Ok(plan);
    }

    [HttpPost("optimizations/{id:guid}/apply")]
    public async Task<ActionResult<OptimizationResultModel>> Apply(
        Guid id,
        [FromBody] ApplyOptimizationRequestModel request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Aplicando decisões para plano de otimização {OptimizationId} (versão enviada: {Version}, decisões: {Count})",
            id, request.Version, request.Decisions.Count);

        var command = new ApplyOptimizationCommand(request.Version, request.Decisions);
        var result = await service.ApplyDecisionsAsync(id, command, cancellationToken);

        logger.LogInformation("Decisões aplicadas ao plano {OptimizationId}. Total de itens aplicados: {AppliedCount}",
            id, result.AppliedChanges.Count);

        return Ok(result);
    }
}
