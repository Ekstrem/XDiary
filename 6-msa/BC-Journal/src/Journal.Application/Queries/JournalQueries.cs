namespace Journal.Application.Queries;
using Journal.InternalContracts;
using MediatR;
public sealed record ListRecordsQuery : IRequest<IReadOnlyList<JournalReadModel>>;
public sealed record GetRecordByIdQuery(Guid AggregateId) : IRequest<JournalReadModel?>;

public sealed class ListRecordsHandler : IRequestHandler<ListRecordsQuery, IReadOnlyList<JournalReadModel>>
{
    private readonly IJournalQueryRepository _repo;
    public ListRecordsHandler(IJournalQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<JournalReadModel>> Handle(ListRecordsQuery r, CancellationToken ct) => _repo.ListAsync(ct);
}
public sealed class GetRecordByIdHandler : IRequestHandler<GetRecordByIdQuery, JournalReadModel?>
{
    private readonly IJournalQueryRepository _repo;
    public GetRecordByIdHandler(IJournalQueryRepository repo) => _repo = repo;
    public Task<JournalReadModel?> Handle(GetRecordByIdQuery r, CancellationToken ct) => _repo.GetByIdAsync(r.AggregateId, ct);
}