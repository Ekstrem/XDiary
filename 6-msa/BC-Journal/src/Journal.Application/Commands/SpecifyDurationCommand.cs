using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: указать длительность (шаг визарда «Длительность»).</summary>
public class SpecifyDurationCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public int DurationMin { get; set; }
}
