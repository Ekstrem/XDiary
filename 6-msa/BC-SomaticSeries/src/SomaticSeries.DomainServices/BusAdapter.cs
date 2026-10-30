namespace SomaticSeries.DomainServices;
using SomaticSeries.Domain;
using XDiary.Contracts;
public sealed class BusAdapter : IBusAdapter<ISomaticSeries>
{ public Task Publish<TEvent>(TEvent @event, CancellationToken ct = default) => Task.CompletedTask; }