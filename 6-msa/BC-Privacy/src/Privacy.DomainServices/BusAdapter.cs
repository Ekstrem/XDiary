namespace Privacy.DomainServices;
using Privacy.Domain;
using XDiary.Contracts;
public sealed class BusAdapter : IBusAdapter<IPrivacy>
{ public Task Publish<TEvent>(TEvent @event, CancellationToken ct = default) => Task.CompletedTask; }