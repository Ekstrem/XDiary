using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using Catalog.Domain.Abstraction;

namespace Catalog.DomainServices;

internal static class NotifierExtension
{
    public static ICatalogItemAggregate PipeNotifierToContract(
        this ICatalogItemAggregate aggregate, List<IObserver<AggregateResult<ICatalog, IAnemicModel<ICatalog>>>> observers)
        => Notifier.Create(aggregate, observers);

    public static ICatalogItemAggregate PipeNotifierToContract(
        this ICatalogItemAggregate aggregate, params IObserver<AggregateResult<ICatalog, IAnemicModel<ICatalog>>>[] observers)
        => Notifier.Create(aggregate, observers.ToList());
}
