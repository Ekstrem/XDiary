namespace Catalog.DomainServices;
using Catalog.Domain;
using XDiary.Contracts;
public sealed class BusAdapter : IBusAdapter<ICatalog>
{ public Task Publish<TEvent>(TEvent @event, CancellationToken ct = default) => Task.CompletedTask; }