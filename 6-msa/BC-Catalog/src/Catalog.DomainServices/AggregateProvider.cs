namespace Catalog.DomainServices;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;
public sealed class AggregateProvider { public object Create(ICatalogAnemicModel model) => Aggregate.CreateInstance(model); }