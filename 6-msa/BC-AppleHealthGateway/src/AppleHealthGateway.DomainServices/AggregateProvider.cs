namespace AppleHealthGateway.DomainServices;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.Domain.Implementation;
public sealed class AggregateProvider { public object Create(IAppleHealthGatewayAnemicModel model) => Aggregate.CreateInstance(model); }