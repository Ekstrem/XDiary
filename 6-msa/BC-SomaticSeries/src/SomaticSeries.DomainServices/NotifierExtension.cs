using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;

namespace SomaticSeries.DomainServices;

internal static class NotifierExtension
{
    public static ISomaticPointAggregate PipeNotifierToContract(
        this ISomaticPointAggregate aggregate, List<IObserver<AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>>>> observers)
        => Notifier.Create(aggregate, observers);

    public static ISomaticPointAggregate PipeNotifierToContract(
        this ISomaticPointAggregate aggregate, params IObserver<AggregateResult<ISomaticSeries, IAnemicModel<ISomaticSeries>>>[] observers)
        => Notifier.Create(aggregate, observers.ToList());
}
