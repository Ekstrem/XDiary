namespace DailyMetrics.Application.Queries;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using DailyMetrics.Domain;
using DailyMetrics.InternalContracts;
public sealed record GetDailySeriesQuery(Guid ClientId, string FromDate, string ToDate) : IRequest<IReadOnlyList<DailyMetricsReadModel>>;
public sealed class GetDailySeriesHandler : IRequestHandler<GetDailySeriesQuery, IReadOnlyList<DailyMetricsReadModel>>, IQueryHandler
{
    private readonly IReadRepository<IDailyMetrics, DailyMetricsReadModel> _read;
    public GetDailySeriesHandler(IReadRepository<IDailyMetrics, DailyMetricsReadModel> read) => _read = read;
    public async Task<IReadOnlyList<DailyMetricsReadModel>> Handle(GetDailySeriesQuery r, CancellationToken ct)
        => (await _read.GetAllAsync(new Paging(1, int.MaxValue), ct)).Where(x => x.ClientId == r.ClientId && string.Compare(x.Date, r.FromDate) >= 0 && string.Compare(x.Date, r.ToDate) <= 0).ToList();
}
