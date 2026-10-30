using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: указать место (шаг визарда «Место»).</summary>
public class SpecifyPlaceCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public string Place { get; set; } = string.Empty;
}
