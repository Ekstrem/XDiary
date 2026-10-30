namespace SomaticSeries.Application.Queries;
using MediatR;
using SomaticSeries.InternalContracts;
public sealed record GetSomaticSliceQuery(string FromT, string ToT) : IRequest<IReadOnlyList<SomaticSeriesReadModel>>;
public sealed class GetSomaticSliceHandler : IRequestHandler<GetSomaticSliceQuery, IReadOnlyList<SomaticSeriesReadModel>>
{
    private readonly ISomaticSeriesQueryRepository _repo;
    public GetSomaticSliceHandler(ISomaticSeriesQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<SomaticSeriesReadModel>> Handle(GetSomaticSliceQuery r, CancellationToken ct) => _repo.GetSliceAsync(r.FromT, r.ToT, ct);
}