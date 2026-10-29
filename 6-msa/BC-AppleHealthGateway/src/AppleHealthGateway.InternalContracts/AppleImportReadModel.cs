using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;

namespace AppleHealthGateway.InternalContracts;

/// <summary>
/// Материализованное представление состояния импорта Apple Health (проекция в Read Store).
/// </summary>
public class AppleImportReadModel : IReadModel<IAppleHealthGateway>
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public List<string> Metrics { get; set; } = new();
    public string LastImport { get; set; } = string.Empty;
    public List<string> ProcessedFiles { get; set; } = new();

    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
