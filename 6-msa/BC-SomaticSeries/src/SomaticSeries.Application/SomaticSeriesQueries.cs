namespace SomaticSeries.Application.Queries;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using SomaticSeries.Domain;
using SomaticSeries.InternalContracts;
public sealed record GetSomaticSliceQuery(string FromT, string ToT, Guid ClientId) : IRequest<IReadOnlyList<SomaticSeriesReadModel>>;
public sealed class GetSomaticSliceHandler : IRequestHandler<GetSomaticSliceQuery, IReadOnlyList<SomaticSeriesReadModel>>, IQueryHandler
{
    private readonly IReadRepository<ISomaticSeries, SomaticSeriesReadModel> _read;
    public GetSomaticSliceHandler(IReadRepository<ISomaticSeries, SomaticSeriesReadModel> read) => _read = read;
    public async Task<IReadOnlyList<SomaticSeriesReadModel>> Handle(GetSomaticSliceQuery r, CancellationToken ct)
        => (await _read.GetAllAsync(new Paging(1, int.MaxValue), ct)).Where(x => x.ClientId == r.ClientId && string.Compare(x.T, r.FromT) >= 0 && string.Compare(x.T, r.ToT) <= 0).ToList();
}
