namespace Partners.Application.Queries;
using MediatR;
using Partners.InternalContracts;
public sealed record ListPartnersQuery : IRequest<IReadOnlyList<PartnersReadModel>>;
public sealed record GetPartnerByIdQuery(Guid AggregateId) : IRequest<PartnersReadModel?>;
public sealed class ListPartnersHandler : IRequestHandler<ListPartnersQuery, IReadOnlyList<PartnersReadModel>>
{
    private readonly IPartnersQueryRepository _repo;
    public ListPartnersHandler(IPartnersQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<PartnersReadModel>> Handle(ListPartnersQuery r, CancellationToken ct) => _repo.ListAsync(ct);
}
public sealed class GetPartnerByIdHandler : IRequestHandler<GetPartnerByIdQuery, PartnersReadModel?>
{
    private readonly IPartnersQueryRepository _repo;
    public GetPartnerByIdHandler(IPartnersQueryRepository repo) => _repo = repo;
    public Task<PartnersReadModel?> Handle(GetPartnerByIdQuery r, CancellationToken ct) => _repo.GetByIdAsync(r.AggregateId, ct);
}