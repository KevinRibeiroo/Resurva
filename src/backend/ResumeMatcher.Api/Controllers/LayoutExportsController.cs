using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ResumeMatcher.Application;

namespace ResumeMatcher.Api.Controllers;

[ApiController]
[Route("api/optimizations/{id:guid}/layout")]
[EnableRateLimiting(ApiRateLimitOptions.PolicyName)]
[RequestSizeLimit(LayoutExportConstraints.MaxRequestBytes)]
[RequestFormLimits(MemoryBufferThreshold = LayoutExportConstraints.MaxRequestBytes,
    MultipartBodyLengthLimit = LayoutExportConstraints.MaxDocumentBytes,
    ValueLengthLimit = LayoutExportConstraints.MaxFormValueBytes, ValueCountLimit = 8)]
public sealed class LayoutExportsController(ILayoutPreservingExportService service, IHostEnvironment environment) : ControllerBase
{
    [HttpPost("inspect")]
    public async Task<ActionResult<LayoutInspectionModel>> Inspect(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Ok(await service.InspectAsync(id, await ReadAsync(file, cancellationToken), cancellationToken));
    }

    [HttpPost("export")]
    public async Task<IActionResult> Export(Guid id, IFormFile file, [FromForm] int version,
        [FromForm] string sourceSha256, [FromForm] string placements, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment()) return NotFound();
        Response.Headers.CacheControl = "no-store";
        LayoutPlacementModel[] targets;
        try
        {
            targets = JsonSerializer.Deserialize<LayoutPlacementModel[]>(placements,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
                ?? throw new JsonException();
            if (targets.Any(t => t is null || string.IsNullOrWhiteSpace(t.BlockId))) throw new JsonException();
        }
        catch (JsonException) { return Problem(statusCode: 400, title: "Destinos inválidos. Inspecione o DOCX novamente."); }
        var result = await service.ExportAsync(id, await ReadAsync(file, cancellationToken), version, sourceSha256, targets, cancellationToken);
        return File(result, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "curriculo-adaptado-layout-original.docx");
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken ct)
    {
        if (!Path.GetExtension(file.FileName).Equals(".docx", StringComparison.OrdinalIgnoreCase) || file.Length <= 0 || file.Length > LayoutExportConstraints.MaxDocumentBytes)
            throw new InvalidResumeException("Selecione o DOCX original, com até 10 MiB.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
