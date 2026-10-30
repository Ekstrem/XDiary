namespace AppleHealthGateway.Storage;
public sealed class AppleImportEntity
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string MetricsJson { get; set; } = "[]";
    public string LastImport { get; set; } = string.Empty;
    public string ProcessedFilesJson { get; set; } = "[]";
    public long Version { get; set; }
}