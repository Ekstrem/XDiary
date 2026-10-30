using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: добавить партнёра (шаг визарда «Партнёр», 0..N).</summary>
public class AddPartnerCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public string PartnerId { get; set; } = string.Empty;
}
