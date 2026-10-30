using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: завершить запись (шаг визарда «Итог»).</summary>
public class CompleteCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
}
