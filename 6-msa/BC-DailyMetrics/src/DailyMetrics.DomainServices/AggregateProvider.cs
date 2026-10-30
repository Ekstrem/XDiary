namespace DailyMetrics.DomainServices;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;
public sealed class AggregateProvider { public object Create(IDailyMetricsAnemicModel model) => Aggregate.CreateInstance(model); }