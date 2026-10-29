using Catalog.InternalContracts;
using MediatR;

namespace Catalog.Application.Queries;

/// <summary>Р—Р°РїСЂРѕСЃ: Р¶СѓСЂРЅР°Р» (СЃРїРёСЃРѕРє Р·Р°РїРёСЃРµР№), СЃРѕСЂС‚РёСЂРѕРІРєР° РїРѕ startedAt DESC.</summary>
public class ListCatalogItemsQuery : IRequest<List<CatalogReadModel>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 1000;
}
