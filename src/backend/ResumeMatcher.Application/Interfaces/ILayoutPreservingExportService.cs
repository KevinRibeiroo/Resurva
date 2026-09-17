namespace ResumeMatcher.Application;

public interface ILayoutPreservingExportService
{
    Task<LayoutInspectionModel> InspectAsync(Guid optimizationId, byte[] original, CancellationToken cancellationToken);
    Task<byte[]> ExportAsync(Guid optimizationId, byte[] original, int version, string sourceSha256,
        IReadOnlyList<LayoutPlacementModel> placements, CancellationToken cancellationToken);
}
