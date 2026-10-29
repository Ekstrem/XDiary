using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using SomaticSeries.Domain;
using SomaticSeries.InternalContracts;
using MediatR;

namespace SomaticSeries.Application.Queries;

public class GetSomaticSliceHandler : IRequestHandler<GetSomaticSliceQuery, List<SomaticSeriesReadModel>>
{
    private readonly IReadRepository<ISomaticSeries, SomaticSeriesReadModel> _repository;

    public GetSomaticSliceHandler(IReadRepository<ISomaticSeries, SomaticSeriesReadModel> repository) => _repository = repository;

    public async Task<List<SomaticSeriesReadModel>> Handle(GetSomaticSliceQuery request, CancellationToken ct)
    {
        var items = await _repository.GetAllAsync(new Paging(1, 100000), ct);

        if (!DateTime.TryParse(request.StartedAt, out var start))
            return new List<SomaticSeriesReadModel>();

        var window = Math.Max(1, request.WindowMin);
        var from = start.AddMinutes(-window);
        var to = start.AddMinutes(window);

        return items
            .Where(p => DateTime.TryParse(p.T, out var t) && t >= from && t <= to)
            .OrderBy(p => p.T)
            .ToList();
    }
}
