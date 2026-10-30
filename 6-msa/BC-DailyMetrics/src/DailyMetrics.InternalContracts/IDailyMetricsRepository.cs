namespace DailyMetrics.InternalContracts;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using XDiary.Contracts;
public interface IDailyMetricsRepository : IRepository<IDailyMetrics, IDailyMetricsAnemicModel>
{
    Task<IDailyMetricsAnemicModel?> GetByDateAsync(string date, CancellationToken ct = default);
}