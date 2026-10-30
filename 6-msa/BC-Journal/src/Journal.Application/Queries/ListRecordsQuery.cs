using Journal.InternalContracts;
using MediatR;

namespace Journal.Application.Queries;

/// <summary>Р—Р°РїСЂРѕСЃ: Р¶СѓСЂРЅР°Р» (СЃРїРёСЃРѕРє Р·Р°РїРёСЃРµР№), СЃРѕСЂС‚РёСЂРѕРІРєР° РїРѕ startedAt DESC.</summary>
public class ListRecordsQuery : IRequest<List<JournalReadModel>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 1000;
}
