namespace Catalog.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;

/// <summary>Агрегат каталога: состояние (анемичная модель) + бизнес-операции.</summary>
public interface ICatalogAggregate : IAggregate<ICatalog>, ICatalogAnemicModel
{
    AggregateResult<ICatalog, ICatalogAnemicModel> AddItem(string kind, string name, Guid clientId);
    AggregateResult<ICatalog, ICatalogAnemicModel> Rename(string name);
    AggregateResult<ICatalog, ICatalogAnemicModel> UpdateDetails(string photo, string notes, string brand, string fantasyPartner);
    AggregateResult<ICatalog, ICatalogAnemicModel> Archive();
    AggregateResult<ICatalog, ICatalogAnemicModel> Restore();
    AggregateResult<ICatalog, ICatalogAnemicModel> Remove();
}
