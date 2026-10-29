using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain;
using Journal.Domain.Abstraction;

namespace Journal.DomainServices;

internal static class NotifierExtension
{
    public static IRecordAggregate PipeNotifierToContract(
        this IRecordAggregate aggregate, List<IObserver<AggregateResult<IJournal, IAnemicModel<IJournal>>>> observers)
        => Notifier.Create(aggregate, observers);

    public static IRecordAggregate PipeNotifierToContract(
        this IRecordAggregate aggregate, params IObserver<AggregateResult<IJournal, IAnemicModel<IJournal>>>[] observers)
        => Notifier.Create(aggregate, observers.ToList());
}
