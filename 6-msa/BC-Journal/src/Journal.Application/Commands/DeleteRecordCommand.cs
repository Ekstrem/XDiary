using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: удалить запись.</summary>
public class DeleteRecordCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
}
