using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace SomaticSeries.Domain.Abstraction;

/// <summary>
/// Агрегат SomaticPoint (минутная точка физиологии) контекста «Соматика».
/// Append-only: единственная операция — импорт/переимпорт точки (upsert по t).
/// </summary>
public interface ISomaticPointAggregate : IAggregate<ISomaticSeries>
{
    /// <summary>Импортировать (создать/обновить) минутную точку.</summary>
    AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>> ImportPoint(IAnemicModel<ISomaticSeries> model, ICommandToAggregate commandMetadata);
}
