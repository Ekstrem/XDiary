namespace Catalog.InternalContracts;
public interface ICatalogQueryRepository
{
    Task<CatalogReadModel?> GetByIdAsync(Guid id, Guid clientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalogReadModel>> ListAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}