using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
namespace Catalog.InternalContracts;
using XDiary.Contracts;
public sealed class CatalogReadModel : IReadModel<Catalog.Domain.ICatalog>
{
    public Guid Id { get; set; }
    // BE-138: версия события для stale-skip (QG-305) — без неё повторное/старое событие перезапишет read-модель.
    public long Version { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Photo { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public bool Archived { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string FantasyPartner { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
}