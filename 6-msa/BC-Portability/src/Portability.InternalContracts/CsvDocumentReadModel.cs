using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;

namespace Portability.InternalContracts;

/// <summary>
/// Материализованное представление CSV-документа переноса (проекция в Read Store).
/// </summary>
public class CsvDocumentReadModel : IReadModel<IPortability>
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<string> Rows { get; set; } = new();

    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
