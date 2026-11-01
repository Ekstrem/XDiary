namespace Client.DomainServices;
using Client.Domain;
using XDiary.Contracts;
public sealed class BusAdapter : IBusAdapter<IClients>
{
    public Task Publish<TEvent>(TEvent @event, CancellationToken ct = default) => Task.CompletedTask;
}