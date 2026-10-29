using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: импортировать запись (upsert по id).</summary>
public class ImportRecordCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public RecordDraft Draft { get; set; } = new();
}
