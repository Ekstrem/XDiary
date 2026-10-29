using Analytics.Domain.Models;
using MediatR;

namespace Analytics.Application.Queries;

public class GetSummaryHandler : IRequestHandler<GetSummaryQuery, SummaryCards>
{
    public Task<SummaryCards> Handle(GetSummaryQuery request, CancellationToken ct)
        => Task.FromResult(SummaryCalculator.Calculate(request.Period, request.Records));
}
