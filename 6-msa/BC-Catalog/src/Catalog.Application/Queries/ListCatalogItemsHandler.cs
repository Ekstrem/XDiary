using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Catalog.Domain;
using Catalog.InternalContracts;
using MediatR;

namespace Catalog.Application.Queries;

public class ListCatalogItemsHandler : IRequestHandler<ListCatalogItemsQuery, List<CatalogReadModel>>
{
    private readonly IReadRepository<ICatalog, CatalogReadModel> _repository;

    public ListCatalogItemsHandler(IReadRepository<ICatalog, CatalogReadModel> repository) => _repository = repository;

    public async Task<List<CatalogReadModel>> Handle(ListCatalogItemsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 10000);

        var items = await _repository.GetAllAsync(new Paging(page, size), ct);
        return items.OrderBy(r => r.Name).ToList();
    }
}
