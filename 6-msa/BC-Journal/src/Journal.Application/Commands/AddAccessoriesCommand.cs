using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: добавить аксессуары (шаг визарда «Аксессуары»).</summary>
public class AddAccessoriesCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public List<string> Costumes { get; set; } = new();
    public List<string> Toys { get; set; } = new();
    public List<string> Fantasies { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}
