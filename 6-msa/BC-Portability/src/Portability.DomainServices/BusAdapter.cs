namespace Portability.DomainServices;
using Portability.Domain;
using XDiary.Contracts;
public sealed class BusAdapter : IBusAdapter<IPortability>
{ public Task Publish<TEvent>(TEvent @event, CancellationToken ct = default) => Task.CompletedTask; }