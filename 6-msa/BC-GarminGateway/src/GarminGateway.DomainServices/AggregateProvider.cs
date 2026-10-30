namespace GarminGateway.DomainServices;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;
public sealed class AggregateProvider { public object Create(IGarminGatewayAnemicModel model) => Aggregate.CreateInstance(model); }