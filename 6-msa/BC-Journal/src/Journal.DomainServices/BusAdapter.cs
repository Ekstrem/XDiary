namespace Journal.DomainServices;
using Journal.Domain;
using XDiary.Contracts;
public sealed class BusAdapter : IBusAdapter<IJournal>
{
    public Task Publish<TEvent>(TEvent @event, CancellationToken ct = default) => Task.CompletedTask;
}