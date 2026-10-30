using Journal.InternalContracts;
using MediatR;

namespace Journal.Application.Queries;

/// <summary>Р—Р°РїСЂРѕСЃ: РїРѕР»СѓС‡РёС‚СЊ РґРµС‚Р°Р»СЊ Р·Р°РїРёСЃРё РїРѕ РёРґРµРЅС‚РёС„РёРєР°С‚РѕСЂСѓ.</summary>
public class GetRecordByIdQuery : IRequest<JournalReadModel?>
{
    public Guid AggregateId { get; init; }
}
