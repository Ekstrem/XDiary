using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: зафиксировать оргазмы (шаг визарда «Оргазмы»).</summary>
public class RecordOrgasmsCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public int OrgasmsMe { get; set; }
    public int OrgasmsPartner { get; set; }
    public List<string> OrgasmMeType { get; set; } = new();
    public List<string> OrgasmPartnerType { get; set; } = new();
    public int OrgasmMeRating { get; set; }
    public int OrgasmPartnerRating { get; set; }
}
