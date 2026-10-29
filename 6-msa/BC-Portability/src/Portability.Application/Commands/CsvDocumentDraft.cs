namespace Portability.Application.Commands;

/// <summary>
/// Черновик CSV-документа переноса (CsvDocument).
/// </summary>
public class CsvDocumentDraft
{
    public string SourceId { get; set; } = "records";
    public List<string> Headers { get; set; } = new();
    public List<string> Rows { get; set; } = new();
}
