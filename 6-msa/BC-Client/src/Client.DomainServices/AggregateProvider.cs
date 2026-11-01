namespace Client.DomainServices;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
public sealed class AggregateProvider
{
    public object Create(IClientAnemicModel model) => Aggregate.CreateInstance(model);
}