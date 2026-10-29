namespace AppleHealthGateway.Application.Commands;

/// <summary>
/// Черновик состояния импорта Apple Health (AppleImport).
/// </summary>
public class AppleImportDraft
{
    public string SourceId { get; set; } = "apple-health";
    public List<string> Metrics { get; set; } = new();
    public string LastImport { get; set; } = string.Empty;
    public List<string> ProcessedFiles { get; set; } = new();
}
