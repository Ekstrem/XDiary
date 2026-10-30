namespace Catalog.Application.Queries;
using MediatR;
using Catalog.InternalContracts;
public sealed record ListCatalogItemsQuery : IRequest<IReadOnlyList<CatalogReadModel>>;
public sealed record GetCatalogItemByIdQuery(Guid AggregateId) : IRequest<CatalogReadModel?>;
public sealed class ListCatalogItemsHandler : IRequestHandler<ListCatalogItemsQuery, IReadOnlyList<CatalogReadModel>>
{
    private readonly ICatalogQueryRepository _repo;
    public ListCatalogItemsHandler(ICatalogQueryRepository repo) => _repo = repo;
    public Task<IReadOnlyList<CatalogReadModel>> Handle(ListCatalogItemsQuery r, CancellationToken ct) => _repo.ListAsync(ct);
}
public sealed class GetCatalogItemByIdHandler : IRequestHandler<GetCatalogItemByIdQuery, CatalogReadModel?>
{
    private readonly ICatalogQueryRepository _repo;
    public GetCatalogItemByIdHandler(ICatalogQueryRepository repo) => _repo = repo;
    public Task<CatalogReadModel?> Handle(GetCatalogItemByIdQuery r, CancellationToken ct) => _repo.GetByIdAsync(r.AggregateId, ct);
}