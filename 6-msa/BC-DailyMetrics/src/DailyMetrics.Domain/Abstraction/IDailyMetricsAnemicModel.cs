namespace DailyMetrics.Domain.Abstraction;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
public interface IDailyMetricsAnemicModel : IAnemicModel<IDailyMetrics>
{
    IDailyMetricsRoot Root { get; }
    int Stress { get; }
    int RestingHr { get; }
    int Hrv { get; }
    ISleep Sleep { get; }
}