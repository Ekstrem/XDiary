namespace Privacy.DomainServices;
using Privacy.Domain.Abstraction;
using Privacy.Domain.Implementation;
public sealed class AggregateProvider { public object Create(IPinCredentialAnemicModel model) => Aggregate.CreateInstance(model); }