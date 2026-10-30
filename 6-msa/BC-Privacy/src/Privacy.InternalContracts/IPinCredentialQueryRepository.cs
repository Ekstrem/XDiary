namespace Privacy.InternalContracts;
public interface IPinCredentialQueryRepository
{
    Task<PinCredentialReadModel?> GetBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default);
}