using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
namespace Portability.InternalContracts;
using XDiary.Contracts;
public sealed class CsvDocumentReadModel : IReadModel<Portability.Domain.IPortability>
{
    public Guid Id { get; set; }
    // BE-140: версия события для stale-skip (QG-305).
    public long Version { get; set; }
    public IReadOnlyList<string> Headers { get; set; } = new List<string>();
    public IReadOnlyList<string> Rows { get; set; } = new List<string>();
    public Guid? ClientId { get; set; }
}