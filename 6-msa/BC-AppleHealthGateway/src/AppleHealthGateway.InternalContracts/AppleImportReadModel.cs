using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
namespace AppleHealthGateway.InternalContracts;
using XDiary.Contracts;
public sealed class AppleImportReadModel : IReadModel<AppleHealthGateway.Domain.IAppleHealthGateway>
{
    public Guid Id { get; set; }
    // BE-140: версия события для stale-skip (QG-305).
    public long Version { get; set; }
    public Guid ClientId { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public IReadOnlyList<string> Metrics { get; set; } = new List<string>();
    public string LastImport { get; set; } = string.Empty;
    public IReadOnlyList<string> ProcessedFiles { get; set; } = new List<string>();
}