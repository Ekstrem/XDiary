namespace Privacy.InternalContracts;
public interface IPinCredentialQueryRepository
{
    Task<PinCredentialReadModel?> GetBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<PinCredentialReadModel?> GetByClientIdSourceIdAsync(Guid clientId, string sourceId, CancellationToken cancellationToken = default);
}