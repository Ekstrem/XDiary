using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using Privacy.Domain.Abstraction;

namespace Privacy.DomainServices;

internal static class NotifierExtension
{
    public static IPinCredentialAggregate PipeNotifierToContract(
        this IPinCredentialAggregate aggregate, List<IObserver<AggregateResult<IPrivacy, IAnemicModel<IPrivacy>>>> observers)
        => Notifier.Create(aggregate, observers);

    public static IPinCredentialAggregate PipeNotifierToContract(
        this IPinCredentialAggregate aggregate, params IObserver<AggregateResult<IPrivacy, IAnemicModel<IPrivacy>>>[] observers)
        => Notifier.Create(aggregate, observers.ToList());
}
