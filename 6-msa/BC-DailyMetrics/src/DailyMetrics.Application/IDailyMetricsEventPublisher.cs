namespace DailyMetrics.Application;
public interface IDailyMetricsEventPublisher
{
    Task PublishStressAsync(string date, int stress, CancellationToken ct = default);
    Task PublishSleepAsync(string date, int score, CancellationToken ct = default);
}