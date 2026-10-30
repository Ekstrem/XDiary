using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: зафиксировать контрацепцию (шаг визарда «Контрацепция»).</summary>
public class RecordContraceptionCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public List<string> Contraceptives { get; set; } = new();
    public int ContraceptiveCount { get; set; }
}
