namespace AppleHealthGateway.InternalContracts;
public interface IAppleHealthGatewayQueryRepository
{
    Task<AppleImportReadModel?> GetBySourceIdAsync(string sourceId, CancellationToken cancellationToken = default);
    Task<AppleImportReadModel?> GetByClientIdSourceIdAsync(Guid clientId, string sourceId, CancellationToken cancellationToken = default);
}