namespace AppleHealthGateway.InternalContracts;
using XDiary.Contracts;
public sealed class AppleImportReadModel : IReadModel<AppleHealthGateway.Domain.IAppleHealthGateway>
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public IReadOnlyList<string> Metrics { get; set; } = new List<string>();
    public string LastImport { get; set; } = string.Empty;
    public IReadOnlyList<string> ProcessedFiles { get; set; } = new List<string>();
}