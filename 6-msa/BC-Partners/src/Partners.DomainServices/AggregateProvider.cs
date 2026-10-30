namespace Partners.DomainServices;
using Partners.Domain.Abstraction;
using Partners.Domain.Implementation;
public sealed class AggregateProvider
{
    public object Create(IPartnerAnemicModel model) => Aggregate.CreateInstance(model);
}