using Partners.InternalContracts;
using MediatR;

namespace Partners.Application.Queries;

/// <summary>Р—Р°РїСЂРѕСЃ: РїРѕР»СѓС‡РёС‚СЊ РґРµС‚Р°Р»СЊ Р·Р°РїРёСЃРё РїРѕ РёРґРµРЅС‚РёС„РёРєР°С‚РѕСЂСѓ.</summary>
public class GetPartnerByIdQuery : IRequest<PartnersReadModel?>
{
    public Guid AggregateId { get; init; }
}
