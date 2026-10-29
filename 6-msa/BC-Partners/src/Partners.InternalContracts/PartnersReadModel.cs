using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;

namespace Partners.InternalContracts;

/// <summary>
/// Материализованное представление партнёра (проекция в Read Store).
/// Колонки коррелируют с 05-materialized-views.md (store `partners`).
/// </summary>
public class PartnersReadModel : IReadModel<IPartners>
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Photo { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
