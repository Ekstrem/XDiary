using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace Portability.Domain.Abstraction;

/// <summary>
/// Агрегат Portability (минутная точка физиологии) контекста «Соматика».
/// Append-only: единственная операция — импорт/переимпорт точки (upsert по t).
/// </summary>
public interface ICsvDocumentAggregate : IAggregate<IPortability>
{
    /// <summary>Импортировать (создать/обновить) минутную точку.</summary>
    AggregateResult<IPortability, IAnemicModel<IPortability>> UpsertDocument(IAnemicModel<IPortability> model, ICommandToAggregate commandMetadata);
}
