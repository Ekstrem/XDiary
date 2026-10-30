using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: добавить позы (шаг визарда «Позы»).</summary>
public class AddPositionsCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public List<string> Positions { get; set; } = new();
}
