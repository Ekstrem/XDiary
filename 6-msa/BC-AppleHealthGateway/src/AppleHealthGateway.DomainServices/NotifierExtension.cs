using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;

namespace AppleHealthGateway.DomainServices;

internal static class NotifierExtension
{
    public static IAppleImportAggregate PipeNotifierToContract(
        this IAppleImportAggregate aggregate, List<IObserver<AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>>> observers)
        => Notifier.Create(aggregate, observers);

    public static IAppleImportAggregate PipeNotifierToContract(
        this IAppleImportAggregate aggregate, params IObserver<AggregateResult<IAppleHealthGateway, IAnemicModel<IAppleHealthGateway>>>[] observers)
        => Notifier.Create(aggregate, observers.ToList());
}
