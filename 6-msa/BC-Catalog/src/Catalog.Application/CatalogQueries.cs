namespace Catalog.Application.Queries;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using Catalog.Domain;
using Catalog.InternalContracts;
public sealed record ListCatalogItemsQuery(Guid ClientId) : IRequest<IReadOnlyList<CatalogReadModel>>;
public sealed record GetCatalogItemByIdQuery(Guid AggregateId, Guid ClientId) : IRequest<CatalogReadModel?>;
public sealed class ListCatalogItemsHandler : IRequestHandler<ListCatalogItemsQuery, IReadOnlyList<CatalogReadModel>>, IQueryHandler
{
    private readonly IReadRepository<ICatalog, CatalogReadModel> _read;
    public ListCatalogItemsHandler(IReadRepository<ICatalog, CatalogReadModel> read) => _read = read;
    public async Task<IReadOnlyList<CatalogReadModel>> Handle(ListCatalogItemsQuery r, CancellationToken ct)
        => (await _read.GetAllAsync(new Paging(1, int.MaxValue), ct)).Where(x => x.ClientId == r.ClientId && x.DeletedAt is null).OrderBy(x => x.Kind).ThenBy(x => x.Name).ToList();
}
public sealed class GetCatalogItemByIdHandler : IRequestHandler<GetCatalogItemByIdQuery, CatalogReadModel?>, IQueryHandler
{
    private readonly IReadRepository<ICatalog, CatalogReadModel> _read;
    public GetCatalogItemByIdHandler(IReadRepository<ICatalog, CatalogReadModel> read) => _read = read;
    public async Task<CatalogReadModel?> Handle(GetCatalogItemByIdQuery r, CancellationToken ct)
    { var x = await _read.GetByIdAsync(r.AggregateId, ct); return x is not null && x.ClientId == r.ClientId && x.DeletedAt is null ? x : null; }
}
