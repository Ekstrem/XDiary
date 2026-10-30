namespace AppleHealthGateway.DomainServices;
using AppleHealthGateway.Domain;
using XDiary.Contracts;
public sealed class BusAdapter : IBusAdapter<IAppleHealthGateway>
{ public Task Publish<TEvent>(TEvent @event, CancellationToken ct = default) => Task.CompletedTask; }