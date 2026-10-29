using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace AppleHealthGateway.Domain.Abstraction;

/// <summary>
/// Агрегат AppleHealthGateway (минутная точка физиологии) контекста «Соматика».
/// Append-only: единственная операция — импорт/переимпорт точки (upsert по t).
/// </summary>
public interface IAppleImportAggregate : IAggregate<IAppleHealthGateway>
{
    /// <summary>Импортировать (создать/обновить) минутную точку.</summary>
    AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>> UpsertImportState(IAnemicModel<IAppleHealthGateway> model, ICommandToAggregate commandMetadata);
}
