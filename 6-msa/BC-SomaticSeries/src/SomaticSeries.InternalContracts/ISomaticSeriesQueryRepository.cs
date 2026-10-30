namespace SomaticSeries.InternalContracts;
public interface ISomaticSeriesQueryRepository
{
    Task<SomaticSeriesReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SomaticSeriesReadModel>> GetSliceAsync(string fromT, string toT, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}