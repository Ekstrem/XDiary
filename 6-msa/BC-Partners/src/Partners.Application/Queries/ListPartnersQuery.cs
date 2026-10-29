using Partners.InternalContracts;
using MediatR;

namespace Partners.Application.Queries;

/// <summary>Р—Р°РїСЂРѕСЃ: Р¶СѓСЂРЅР°Р» (СЃРїРёСЃРѕРє Р·Р°РїРёСЃРµР№), СЃРѕСЂС‚РёСЂРѕРІРєР° РїРѕ startedAt DESC.</summary>
public class ListPartnersQuery : IRequest<List<PartnersReadModel>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 1000;
}
