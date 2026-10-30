namespace AppleHealthGateway.InternalContracts;
public interface IAppleHealthGatewayQueryRepository
{
    Task<AppleImportReadModel?> GetBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default);
}