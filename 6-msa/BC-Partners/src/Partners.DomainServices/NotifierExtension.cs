using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Partners.Domain.Abstraction;

namespace Partners.DomainServices;

internal static class NotifierExtension
{
    public static IPartnerAggregate PipeNotifierToContract(
        this IPartnerAggregate aggregate, List<IObserver<AggregateResult<IPartners, IAnemicModel<IPartners>>>> observers)
        => Notifier.Create(aggregate, observers);

    public static IPartnerAggregate PipeNotifierToContract(
        this IPartnerAggregate aggregate, params IObserver<AggregateResult<IPartners, IAnemicModel<IPartners>>>[] observers)
        => Notifier.Create(aggregate, observers.ToList());
}
