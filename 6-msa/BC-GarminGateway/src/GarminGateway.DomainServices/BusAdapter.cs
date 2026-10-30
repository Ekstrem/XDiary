namespace GarminGateway.DomainServices;
using GarminGateway.Domain;
using XDiary.Contracts;
public sealed class BusAdapter : IBusAdapter<IGarminGateway>
{ public Task Publish<TEvent>(TEvent @event, CancellationToken ct = default) => Task.CompletedTask; }