using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace GarminGateway.Domain.Abstraction;

/// <summary>
/// Агрегат GarminGateway (минутная точка физиологии) контекста «Соматика».
/// Append-only: единственная операция — импорт/переимпорт точки (upsert по t).
/// </summary>
public interface IGarminSyncAggregate : IAggregate<IGarminGateway>
{
    /// <summary>Импортировать (создать/обновить) минутную точку.</summary>
    AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>> UpsertSyncState(IAnemicModel<IGarminGateway> model, ICommandToAggregate commandMetadata);
}
