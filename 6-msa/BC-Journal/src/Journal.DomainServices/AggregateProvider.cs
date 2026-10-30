namespace Journal.DomainServices;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
public sealed class AggregateProvider
{
    public object Create(IJournalAnemicModel model) => Aggregate.CreateInstance(model);
}