namespace Journal.Application.Queries;
using Journal.InternalContracts;
using MediatR;
public sealed record ListRecordsQuery(int? Limit = null, int? Offset = null) : IRequest<IReadOnlyList<JournalReadModel>>;
public sealed record GetRecordByIdQuery(Guid AggregateId) : IRequest<JournalReadModel?>;
public sealed record RecentDefaultsQuery : IRequest<RecentDefaultsView?>;
public sealed record JournalPlacesQuery : IRequest<IReadOnlyList<string>>;
public sealed record JournalFantasiesQuery : IRequest<IReadOnlyList<string>>;

public sealed class ListRecordsHandler : IRequestHandler<ListRecordsQuery, IReadOnlyList<JournalReadModel>>
{
    private readonly IJournalQueryRepository _repo;
    public ListRecordsHandler(IJournalQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<JournalReadModel>> Handle(ListRecordsQuery r, CancellationToken ct) => _repo.ListAsync(r.Limit, r.Offset, ct);
}
public sealed class GetRecordByIdHandler : IRequestHandler<GetRecordByIdQuery, JournalReadModel?>
{
    private readonly IJournalQueryRepository _repo;
    public GetRecordByIdHandler(IJournalQueryRepository repo) => _repo = repo;
    public Task<JournalReadModel?> Handle(GetRecordByIdQuery r, CancellationToken ct) => _repo.GetByIdAsync(r.AggregateId, ct);
}
public sealed class RecentDefaultsHandler : IRequestHandler<RecentDefaultsQuery, RecentDefaultsView?>
{
    private readonly IJournalQueryRepository _repo;
    public RecentDefaultsHandler(IJournalQueryRepository repo) => _repo = repo;
    public Task<RecentDefaultsView?> Handle(RecentDefaultsQuery r, CancellationToken ct) => _repo.GetRecentAsync(ct);
}
public sealed class JournalPlacesHandler : IRequestHandler<JournalPlacesQuery, IReadOnlyList<string>>
{
    private readonly IJournalQueryRepository _repo;
    public JournalPlacesHandler(IJournalQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<string>> Handle(JournalPlacesQuery r, CancellationToken ct) => _repo.GetPlacesAsync(ct);
}
public sealed class JournalFantasiesHandler : IRequestHandler<JournalFantasiesQuery, IReadOnlyList<string>>
{
    private readonly IJournalQueryRepository _repo;
    public JournalFantasiesHandler(IJournalQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<string>> Handle(JournalFantasiesQuery r, CancellationToken ct) => _repo.GetFantasiesAsync(ct);
}