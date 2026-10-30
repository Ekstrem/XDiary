namespace Partners.DomainServices;
using Partners.Domain;
using XDiary.Contracts;
public sealed class BusAdapter : IBusAdapter<IPartners>
{
    public Task Publish<TEvent>(TEvent @event, CancellationToken ct = default) => Task.CompletedTask;
}