namespace Partners.Application.Queries;
using MediatR;
using Partners.InternalContracts;
public sealed record ListPartnersQuery(Guid ClientId) : IRequest<IReadOnlyList<PartnersReadModel>>;
public sealed record GetPartnerByIdQuery(Guid AggregateId) : IRequest<PartnersReadModel?>;
public sealed class ListPartnersHandler : IRequestHandler<ListPartnersQuery, IReadOnlyList<PartnersReadModel>>
{
    private readonly IPartnersQueryRepository _repo;
    public ListPartnersHandler(IPartnersQueryRepository repo) => _repo = repo;
    public async Task<IReadOnlyList<PartnersReadModel>> Handle(ListPartnersQuery r, CancellationToken ct) => (await _repo.ListAsync(r.ClientId, ct)).Where(x => x.DeletedAt == null).ToList();
}
public sealed class GetPartnerByIdHandler : IRequestHandler<GetPartnerByIdQuery, PartnersReadModel?>
{
    private readonly IPartnersQueryRepository _repo;
    public GetPartnerByIdHandler(IPartnersQueryRepository repo) => _repo = repo;
    public async Task<PartnersReadModel?> Handle(GetPartnerByIdQuery r, CancellationToken ct) { var x = await _repo.GetByIdAsync(r.AggregateId, ct); return x is null || x.DeletedAt is not null ? null : x; }
}