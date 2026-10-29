using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Catalog.Domain;
using Catalog.InternalContracts;
using MediatR;

namespace Catalog.Application.Queries;

public class GetCatalogItemByIdHandler : IRequestHandler<GetCatalogItemByIdQuery, CatalogReadModel?>
{
    private readonly IReadRepository<ICatalog, CatalogReadModel> _repository;

    public GetCatalogItemByIdHandler(IReadRepository<ICatalog, CatalogReadModel> repository) => _repository = repository;

    public Task<CatalogReadModel?> Handle(GetCatalogItemByIdQuery request, CancellationToken ct)
        => _repository.GetByIdAsync(request.AggregateId, ct);
}
