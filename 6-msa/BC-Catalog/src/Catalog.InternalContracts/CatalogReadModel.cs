using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;

namespace Catalog.InternalContracts;

/// <summary>
/// Материализованное представление элемента каталога (проекция в Read Store).
/// Колонки коррелируют с 05-materialized-views.md (store `catalog`).
/// </summary>
public class CatalogReadModel : IReadModel<ICatalog>
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Photo { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public bool Archived { get; set; }
    public string Partner { get; set; } = string.Empty;

    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
