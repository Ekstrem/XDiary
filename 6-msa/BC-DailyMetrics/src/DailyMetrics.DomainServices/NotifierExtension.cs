using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;

namespace DailyMetrics.DomainServices;

internal static class NotifierExtension
{
    public static IDailyMetricsAggregate PipeNotifierToContract(
        this IDailyMetricsAggregate aggregate, List<IObserver<AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>>>> observers)
        => Notifier.Create(aggregate, observers);

    public static IDailyMetricsAggregate PipeNotifierToContract(
        this IDailyMetricsAggregate aggregate, params IObserver<AggregateResult<IDailyMetrics, IAnemicModel<IDailyMetrics>>>[] observers)
        => Notifier.Create(aggregate, observers.ToList());
}
