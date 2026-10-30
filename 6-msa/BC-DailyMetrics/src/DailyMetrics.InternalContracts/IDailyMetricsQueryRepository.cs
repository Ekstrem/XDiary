namespace DailyMetrics.InternalContracts;
public interface IDailyMetricsQueryRepository
{
    Task<IReadOnlyList<DailyMetricsReadModel>> GetSeriesAsync(string fromDate, string toDate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(CancellationToken cancellationToken = default);
}