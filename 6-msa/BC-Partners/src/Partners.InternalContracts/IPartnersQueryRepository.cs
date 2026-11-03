namespace Partners.InternalContracts;
public interface IPartnersQueryRepository
{
    Task<PartnersReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartnersReadModel>> ListAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}