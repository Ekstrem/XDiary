using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: удалить партнёра (шаг визарда «Партнёр»).</summary>
public class RemovePartnerCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public string PartnerId { get; set; } = string.Empty;
}
