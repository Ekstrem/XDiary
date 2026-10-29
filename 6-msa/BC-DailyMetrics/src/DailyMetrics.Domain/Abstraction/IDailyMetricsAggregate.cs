using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace DailyMetrics.Domain.Abstraction;

/// <summary>
/// Агрегат DailyMetrics (минутная точка физиологии) контекста «Соматика».
/// Append-only: единственная операция — импорт/переимпорт точки (upsert по t).
/// </summary>
public interface IDailyMetricsAggregate : IAggregate<IDailyMetrics>
{
    /// <summary>Импортировать (создать/обновить) минутную точку.</summary>
    AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>> ImportDaily(IAnemicModel<IDailyMetrics> model, ICommandToAggregate commandMetadata);
}
