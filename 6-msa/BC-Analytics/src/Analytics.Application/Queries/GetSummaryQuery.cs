using Analytics.Domain.Models;
using MediatR;

namespace Analytics.Application.Queries;

/// <summary>Запрос: сводка за период (30/90/all).</summary>
public class GetSummaryQuery : IRequest<SummaryCards>
{
    public string Period { get; init; } = "30";
    public List<RecordInput> Records { get; init; } = new();
}
