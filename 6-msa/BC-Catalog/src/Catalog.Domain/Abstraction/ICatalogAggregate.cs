namespace Catalog.Domain.Abstraction;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;

/// <summary>Агрегат каталога: состояние (анемичная модель) + бизнес-операции.</summary>
public interface ICatalogAggregate : IAggregate<ICatalog>, ICatalogAnemicModel
{
    AggregateResult<ICatalog, ICatalogAnemicModel> AddItem(string kind, string name, Guid clientId, CommandToAggregate? command = null);
    AggregateResult<ICatalog, ICatalogAnemicModel> Rename(string name, CommandToAggregate? command = null);
    AggregateResult<ICatalog, ICatalogAnemicModel> UpdateDetails(string photo, string notes, string brand, string fantasyPartner, CommandToAggregate? command = null);
    AggregateResult<ICatalog, ICatalogAnemicModel> Archive(CommandToAggregate? command = null);
    AggregateResult<ICatalog, ICatalogAnemicModel> Restore(CommandToAggregate? command = null);
    AggregateResult<ICatalog, ICatalogAnemicModel> Remove(CommandToAggregate? command = null);
}
