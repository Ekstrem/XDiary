using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Portability.Domain;
using Portability.Domain.Abstraction;

namespace Portability.DomainServices;

internal static class NotifierExtension
{
    public static ICsvDocumentAggregate PipeNotifierToContract(
        this ICsvDocumentAggregate aggregate, List<IObserver<AggregateResult<IPortability, IAnemicModel<IPortability>>>> observers)
        => Notifier.Create(aggregate, observers);

    public static ICsvDocumentAggregate PipeNotifierToContract(
        this ICsvDocumentAggregate aggregate, params IObserver<AggregateResult<IPortability, IAnemicModel<IPortability>>>[] observers)
        => Notifier.Create(aggregate, observers.ToList());
}
