namespace GarminGateway.InternalContracts;
public interface IGarminGatewayQueryRepository
{
    Task<GarminSyncReadModel?> GetBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default);
}