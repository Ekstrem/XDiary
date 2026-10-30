using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: выбрать партнёра (шаг визарда «Партнёр»).</summary>
public class ChoosePartnerCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public string PartnerId { get; set; } = string.Empty;
}
