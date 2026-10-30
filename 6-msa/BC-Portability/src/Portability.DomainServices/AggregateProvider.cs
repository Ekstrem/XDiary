namespace Portability.DomainServices;
using Portability.Domain.Abstraction;
using Portability.Domain.Implementation;
public sealed class AggregateProvider { public object Create(ICsvDocumentAnemicModel model) => Aggregate.CreateInstance(model); }