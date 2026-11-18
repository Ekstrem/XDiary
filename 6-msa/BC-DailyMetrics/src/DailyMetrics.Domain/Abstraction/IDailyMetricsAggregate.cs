namespace DailyMetrics.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;

/// <summary>Агрегат DailyMetrics: состояние (анемичная модель) + бизнес-операции.</summary>
public interface IDailyMetricsAggregate : IAggregate<IDailyMetrics>, IDailyMetricsAnemicModel
{
    IDailyMetricsRoot Root { get; }
    int Stress { get; }
    int RestingHr { get; }
    int Hrv { get; }
    ISleep Sleep { get; }
    Guid? ClientId { get; }
    AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordStress(string date, int stress, Guid clientId);
    AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordRestingHr(string date, int restingHr, Guid clientId);
    AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordHrv(string date, int hrv, Guid clientId);
    AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordSleep(string date, ISleep sleep, Guid clientId);
}
