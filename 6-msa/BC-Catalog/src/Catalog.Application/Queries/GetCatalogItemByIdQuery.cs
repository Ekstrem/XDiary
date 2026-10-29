using Catalog.InternalContracts;
using MediatR;

namespace Catalog.Application.Queries;

/// <summary>Р—Р°РїСЂРѕСЃ: РїРѕР»СѓС‡РёС‚СЊ РґРµС‚Р°Р»СЊ Р·Р°РїРёСЃРё РїРѕ РёРґРµРЅС‚РёС„РёРєР°С‚РѕСЂСѓ.</summary>
public class GetCatalogItemByIdQuery : IRequest<CatalogReadModel?>
{
    public Guid AggregateId { get; init; }
}
