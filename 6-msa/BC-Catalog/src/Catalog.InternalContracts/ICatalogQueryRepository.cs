namespace Catalog.InternalContracts;
public interface ICatalogQueryRepository
{
    Task<CatalogReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalogReadModel>> ListAsync(CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}