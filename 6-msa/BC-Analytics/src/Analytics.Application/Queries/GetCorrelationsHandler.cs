using Analytics.Domain.Models;
using MediatR;

namespace Analytics.Application.Queries;

public class GetCorrelationsHandler : IRequestHandler<GetCorrelationsQuery, List<CorrelationResult>>
{
    public Task<List<CorrelationResult>> Handle(GetCorrelationsQuery request, CancellationToken ct)
        => Task.FromResult(CorrelationCalculator.Calculate(request.Records, request.Metrics));
}
