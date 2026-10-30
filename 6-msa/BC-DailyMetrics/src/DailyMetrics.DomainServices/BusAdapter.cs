namespace DailyMetrics.DomainServices;
using DailyMetrics.Domain;
using XDiary.Contracts;
public sealed class BusAdapter : IBusAdapter<IDailyMetrics>
{ public Task Publish<TEvent>(TEvent @event, CancellationToken ct = default) => Task.CompletedTask; }