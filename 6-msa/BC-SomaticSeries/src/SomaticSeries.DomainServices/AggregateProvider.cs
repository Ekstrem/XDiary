namespace SomaticSeries.DomainServices;
using SomaticSeries.Domain.Abstraction;
using SomaticSeries.Domain.Implementation;
public sealed class AggregateProvider { public object Create(ISomaticSeriesAnemicModel model) => Aggregate.CreateInstance(model); }