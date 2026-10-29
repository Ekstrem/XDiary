using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;

namespace GarminGateway.DomainServices;

internal static class NotifierExtension
{
    public static IGarminSyncAggregate PipeNotifierToContract(
        this IGarminSyncAggregate aggregate, List<IObserver<AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>>>> observers)
        => Notifier.Create(aggregate, observers);

    public static IGarminSyncAggregate PipeNotifierToContract(
        this IGarminSyncAggregate aggregate, params IObserver<AggregateResult<IGarminGateway, IAnemicModel<IGarminGateway>>>[] observers)
        => Notifier.Create(aggregate, observers.ToList());
}
