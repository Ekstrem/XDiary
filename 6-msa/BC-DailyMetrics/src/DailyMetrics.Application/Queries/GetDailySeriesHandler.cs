using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using DailyMetrics.Domain;
using DailyMetrics.InternalContracts;
using MediatR;

namespace DailyMetrics.Application.Queries;

public class GetDailySeriesHandler : IRequestHandler<GetDailySeriesQuery, List<DailyMetricsReadModel>>
{
    private readonly IReadRepository<IDailyMetrics, DailyMetricsReadModel> _repository;

    public GetDailySeriesHandler(IReadRepository<IDailyMetrics, DailyMetricsReadModel> repository) => _repository = repository;

    public async Task<List<DailyMetricsReadModel>> Handle(GetDailySeriesQuery request, CancellationToken ct)
    {
        var items = await _repository.GetAllAsync(new Paging(1, 100000), ct);

        var from = DateTime.TryParse(request.FromDate, out var f) ? f : DateTime.MinValue;
        var to = DateTime.TryParse(request.ToDate, out var t) ? t : DateTime.MaxValue;

        return items
            .Where(m => DateTime.TryParse(m.Date, out var d) && d >= from && d <= to)
            .OrderBy(m => m.Date)
            .ToList();
    }
}
