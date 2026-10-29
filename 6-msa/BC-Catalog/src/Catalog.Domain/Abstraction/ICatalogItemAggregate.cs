using DigiTFactory.Libraries.SeedWorks.Characteristics;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace Catalog.Domain.Abstraction;

/// <summary>
/// Агрегат CatalogItem (партнёр) контекста «Партнёры».
/// Методы делегируют в Aggregate&lt;ICatalog&gt;.Operations из AbstractAggregate.
/// </summary>
public interface ICatalogItemAggregate : IAggregate<ICatalog>
{
    /// <summary>Создать профиль партнёра.</summary>
    AggregateResult<ICatalog, IAnemicModel<ICatalog>> CreateItem(IAnemicModel<ICatalog> model);

    /// <summary>Отредактировать профиль партнёра.</summary>
    AggregateResult<ICatalog, IAnemicModel<ICatalog>> UpdateItem(IAnemicModel<ICatalog> model, ICommandToAggregate commandMetadata);

    /// <summary>Удалить партнёра.</summary>
    AggregateResult<ICatalog, IAnemicModel<ICatalog>> DeleteItem(IAnemicModel<ICatalog> model, ICommandToAggregate commandMetadata);
}
