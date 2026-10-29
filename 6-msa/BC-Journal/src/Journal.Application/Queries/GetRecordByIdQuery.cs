using Journal.InternalContracts;
using MediatR;

namespace Journal.Application.Queries;

/// <summary>Запрос: получить деталь записи по идентификатору.</summary>
public class GetRecordByIdQuery : IRequest<JournalReadModel?>
{
    public Guid AggregateId { get; init; }
}
