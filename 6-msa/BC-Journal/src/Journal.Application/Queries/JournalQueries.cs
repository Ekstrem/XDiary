namespace Journal.Application.Queries;
using Journal.InternalContracts;
using MediatR;
public sealed record ListRecordsQuery(Guid ClientId, int? Limit = null, int? Offset = null) : IRequest<IReadOnlyList<JournalReadModel>>;
public sealed record GetRecordByIdQuery(Guid AggregateId) : IRequest<JournalReadModel?>;
public sealed record RecentDefaultsQuery(Guid ClientId) : IRequest<RecentDefaultsView?>;
public sealed record JournalPlacesQuery(Guid ClientId) : IRequest<IReadOnlyList<string>>;
public sealed record JournalFantasiesQuery(Guid ClientId) : IRequest<IReadOnlyList<string>>;

public sealed class ListRecordsHandler : IRequestHandler<ListRecordsQuery, IReadOnlyList<JournalReadModel>>
{
    private readonly IJournalQueryRepository _repo;
    public ListRecordsHandler(IJournalQueryRepository repo) => _repo = repo;
    public async Task<IReadOnlyList<JournalReadModel>> Handle(ListRecordsQuery r, CancellationToken ct)
        => (await _repo.ListAsync(r.ClientId, r.Limit, r.Offset, ct)).Where(x => !x.Deleted).ToList(); // BE-094: список без удалённых
}
public sealed class GetRecordByIdHandler : IRequestHandler<GetRecordByIdQuery, JournalReadModel?>
{
    private readonly IJournalQueryRepository _repo;
    public GetRecordByIdHandler(IJournalQueryRepository repo) => _repo = repo;
    public async Task<JournalReadModel?> Handle(GetRecordByIdQuery r, CancellationToken ct)
    {
        var x = await _repo.GetByIdAsync(r.AggregateId, ct);
        return x is not null && x.Deleted ? null : x; // BE-094: удалённое → 404
    }
}
public sealed class RecentDefaultsHandler : IRequestHandler<RecentDefaultsQuery, RecentDefaultsView?>
{
    private readonly IJournalQueryRepository _repo;
    public RecentDefaultsHandler(IJournalQueryRepository repo) => _repo = repo;
    public Task<RecentDefaultsView?> Handle(RecentDefaultsQuery r, CancellationToken ct) => _repo.GetRecentAsync(r.ClientId, ct);
}
public sealed class JournalPlacesHandler : IRequestHandler<JournalPlacesQuery, IReadOnlyList<string>>
{
    private readonly IJournalQueryRepository _repo;
    public JournalPlacesHandler(IJournalQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<string>> Handle(JournalPlacesQuery r, CancellationToken ct) => _repo.GetPlacesAsync(r.ClientId, ct);
}
public sealed class JournalFantasiesHandler : IRequestHandler<JournalFantasiesQuery, IReadOnlyList<string>>
{
    private readonly IJournalQueryRepository _repo;
    public JournalFantasiesHandler(IJournalQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<string>> Handle(JournalFantasiesQuery r, CancellationToken ct) => _repo.GetFantasiesAsync(r.ClientId, ct);
}