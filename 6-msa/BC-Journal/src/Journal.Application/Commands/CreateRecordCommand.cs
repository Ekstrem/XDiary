using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: создать запись из визарда.</summary>
public class CreateRecordCommand : IRequest<JournalOperationResult>
{
    public RecordDraft Draft { get; set; } = new();
}
