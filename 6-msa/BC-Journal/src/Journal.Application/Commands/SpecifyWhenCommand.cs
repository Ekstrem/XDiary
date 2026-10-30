using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: указать дату (шаг визарда «Дата»).</summary>
public class SpecifyWhenCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public string StartedAt { get; set; } = string.Empty;
}
