namespace Partners.Application.Queries;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using MediatR;
using Partners.Domain;
using Partners.Domain.Abstraction;
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
    private readonly IAnemicModelRepository<IPartners, IPartnerAnemicModel> _anemic;
    public GetPartnerByIdHandler(IPartnersQueryRepository repo, IAnemicModelRepository<IPartners, IPartnerAnemicModel> anemic) { _repo = repo; _anemic = anemic; }
    public async Task<PartnersReadModel?> Handle(GetPartnerByIdQuery r, CancellationToken ct)
    {
        var x = await _repo.GetByIdAsync(r.AggregateId, ct);
        if (x is not null && x.DeletedAt is null) return x;
        // @TL 23:37 — fallback в command-store: read-модель материализуется с лагом (IObserver.OnNext fire-and-forget).
        var agg = (await _anemic.GetById(r.AggregateId, ct)).LastOrDefault();
        if (agg is null || agg.DeletedAt is not null) return null;
        return new PartnersReadModel { Id = agg.Id, Name = agg.Root.Name, Photo = agg.Photo, Gender = agg.Gender, Notes = agg.Notes, ClientId = agg.ClientId, DeletedAt = agg.DeletedAt };
    }
}