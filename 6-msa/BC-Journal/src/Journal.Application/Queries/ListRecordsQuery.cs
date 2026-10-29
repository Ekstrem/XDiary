using Journal.InternalContracts;
using MediatR;

namespace Journal.Application.Queries;

/// <summary>Запрос: журнал (список записей), сортировка по startedAt DESC.</summary>
public class ListRecordsQuery : IRequest<List<JournalReadModel>>
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 1000;
}
