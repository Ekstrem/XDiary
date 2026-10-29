using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using Catalog.Domain.Abstraction;

namespace Catalog.DomainServices;

/// <summary>
/// РџСЂРѕРІР°Р№РґРµСЂ Р°РіСЂРµРіР°С‚Р° CatalogItem.
/// </summary>
public interface ICatalogAggregateProvider : IAggregateProvider<ICatalog, ICatalogItemAggregate> { }
