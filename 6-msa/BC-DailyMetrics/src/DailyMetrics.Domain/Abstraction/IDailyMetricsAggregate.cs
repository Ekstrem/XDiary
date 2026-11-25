namespace DailyMetrics.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Events;
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
    AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordStress(string date, int stress, Guid clientId, CommandToAggregate? command = null);
    AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordRestingHr(string date, int restingHr, Guid clientId, CommandToAggregate? command = null);
    AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordHrv(string date, int hrv, Guid clientId, CommandToAggregate? command = null);
    AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> RecordSleep(string date, ISleep sleep, Guid clientId, CommandToAggregate? command = null);
}
