namespace Journal.InternalContracts;

public interface IJournalQueryRepository
{
    Task<JournalReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JournalReadModel>> ListAsync(CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}
