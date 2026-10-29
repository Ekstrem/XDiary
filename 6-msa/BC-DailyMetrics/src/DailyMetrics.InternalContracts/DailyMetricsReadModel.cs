using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;

namespace DailyMetrics.InternalContracts;

/// <summary>
/// Материализованное представление дневной сводки (проекция в Read Store).
/// Колонки коррелируют с 05-materialized-views.md (stores `weekly` + `sleep`).
/// </summary>
public class DailyMetricsReadModel : IReadModel<IDailyMetrics>
{
    public Guid Id { get; set; }
    public string Date { get; set; } = string.Empty;
    public int Stress { get; set; }
    public int RestingHr { get; set; }
    public int Hrv { get; set; }

    public int SleepScore { get; set; }
    public int SleepDurationMin { get; set; }
    public int DeepMin { get; set; }
    public int LightMin { get; set; }
    public int RemMin { get; set; }
    public int AwakeMin { get; set; }

    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
