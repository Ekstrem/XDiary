namespace Portability.InternalContracts;
public interface IPortabilityQueryRepository
{
    Task<CsvDocumentReadModel?> GetLastAsync(CancellationToken cancellationToken = default);
}