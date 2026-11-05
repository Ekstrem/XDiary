namespace Portability.InternalContracts;
public interface IPortabilityQueryRepository
{
    Task<CsvDocumentReadModel?> GetLastAsync(Guid clientId, CancellationToken cancellationToken = default);
}