using Journal.Domain;
using MediatR;

namespace Journal.Application.Commands;

/// <summary>Команда: зафиксировать оргазмы (шаг визарда «Оргазмы», по участникам).</summary>
public class RecordOrgasmsCommand : IRequest<JournalOperationResult>
{
    public Guid AggregateId { get; set; }
    public List<OrgasmsEntry> Orgasms { get; set; } = new();
}
