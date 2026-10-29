using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: отредактировать запись.</summary>
public class UpdateRecordCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public RecordDraft Draft { get; set; } = new();
}
