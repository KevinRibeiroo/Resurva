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
    IEnumerable<IResumeDocumentExporter> exporters,
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

    [HttpGet("optimizations/{id:guid}/export")]
    public async Task<IActionResult> Export(
        Guid id,
        [FromQuery] string format,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Formato inválido",
                Detail = "O parâmetro de consulta 'format' é obrigatório. Formatos válidos: pdf, docx."
            });
        }

        var exporter = exporters.FirstOrDefault(e => e.Format.Equals(format, StringComparison.OrdinalIgnoreCase));
        if (exporter is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Formato não suportado",
                Detail = $"Formato '{format}' não suportado. Use 'pdf' ou 'docx'."
            });
        }

        logger.LogInformation("Exportando currículo adaptado para plano {OptimizationId} no formato {Format}", id, format);
        var plan = await service.GetPlanAsync(id, cancellationToken);
        if (plan is null)
        {
            logger.LogWarning("Plano de otimização {OptimizationId} não encontrado para exportação", id);
            return NotFound();
        }

        if (plan.Status != "Applied" || string.IsNullOrWhiteSpace(plan.AdaptedText))
        {
            logger.LogWarning("Tentativa de exportação para plano {OptimizationId} que não está no status 'Applied'", id);
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Plano não aplicado",
                Detail = "O currículo precisa ser adaptado e aplicado com decisões antes da exportação."
            });
        }

        var documentBytes = await exporter.ExportAsync(plan.AdaptedText, cancellationToken);
        var fileName = $"curriculo-adaptado{exporter.FileExtension}";

        logger.LogInformation("Exportação concluída para plano {OptimizationId} ({Size} bytes)", id, documentBytes.Length);
        return File(documentBytes, exporter.ContentType, fileName);
    }
}
