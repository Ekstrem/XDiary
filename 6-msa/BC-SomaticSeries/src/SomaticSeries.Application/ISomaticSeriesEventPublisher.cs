namespace SomaticSeries.Application;
public interface ISomaticSeriesEventPublisher
{
    Task PublishPointAsync(string t, int hr, int bodyBattery, CancellationToken ct = default);
}