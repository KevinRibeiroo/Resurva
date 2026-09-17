namespace ResumeMatcher.Application;

public static class LayoutExportConstraints
{
    public const int MaxDocumentBytes = 10 * 1024 * 1024;
    // Transport envelope only; this does not increase the accepted document size.
    public const int MaxFormValueBytes = 64 * 1024;
    public const int MaxRequestBytes = MaxDocumentBytes + MaxFormValueBytes;
}
