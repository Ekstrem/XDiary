using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;

namespace SomaticSeries.InternalContracts;

/// <summary>
/// Материализованное представление минутной точки соматики (проекция в Read Store).
/// Колонки коррелируют с 05-materialized-views.md (store `somatic`, keyPath `t`).
/// </summary>
public class SomaticSeriesReadModel : IReadModel<ISomaticSeries>
{
    public Guid Id { get; set; }
    public string T { get; set; } = string.Empty;
    public int Hr { get; set; }
    public int BodyBattery { get; set; }

    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
