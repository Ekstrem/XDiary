namespace Partners.Application.Queries;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.InternalContracts;
public sealed record ListPartnersQuery(Guid ClientId) : IRequest<IReadOnlyList<PartnersReadModel>>;
public sealed record GetPartnerByIdQuery(Guid AggregateId, Guid ClientId) : IRequest<PartnersReadModel?>;
public sealed class ListPartnersHandler : IRequestHandler<ListPartnersQuery, IReadOnlyList<PartnersReadModel>>, IQueryHandler
{
    private readonly IReadRepository<IPartners, PartnersReadModel> _read;
    public ListPartnersHandler(IReadRepository<IPartners, PartnersReadModel> read) => _read = read;
    public async Task<IReadOnlyList<PartnersReadModel>> Handle(ListPartnersQuery r, CancellationToken ct)
        => (await _read.GetAllAsync(new Paging(0, int.MaxValue), ct)).Where(x => x.ClientId == r.ClientId && x.DeletedAt == null).ToList();
}
public sealed class GetPartnerByIdHandler : IRequestHandler<GetPartnerByIdQuery, PartnersReadModel?>, IQueryHandler
{
    private readonly IReadRepository<IPartners, PartnersReadModel> _read;
    public GetPartnerByIdHandler(IReadRepository<IPartners, PartnersReadModel> read) => _read = read;
    public async Task<PartnersReadModel?> Handle(GetPartnerByIdQuery r, CancellationToken ct)
    {
        var x = await _read.GetByIdAsync(r.AggregateId, ct);
        return x is not null && x.ClientId == r.ClientId && x.DeletedAt is null ? x : null; // BE-085: чужой/удалённый → null → 404
    }
}
