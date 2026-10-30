namespace DailyMetrics.Application.Queries;
using MediatR;
using DailyMetrics.InternalContracts;
public sealed record GetDailySeriesQuery(string FromDate, string ToDate) : IRequest<IReadOnlyList<DailyMetricsReadModel>>;
public sealed class GetDailySeriesHandler : IRequestHandler<GetDailySeriesQuery, IReadOnlyList<DailyMetricsReadModel>>
{
    private readonly IDailyMetricsQueryRepository _repo;
    public GetDailySeriesHandler(IDailyMetricsQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<DailyMetricsReadModel>> Handle(GetDailySeriesQuery r, CancellationToken ct) => _repo.GetSeriesAsync(r.FromDate, r.ToDate, ct);
}