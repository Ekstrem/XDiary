namespace Catalog.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using MediatR;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using XDiary.Contracts;

public sealed class AddCatalogItemHandler : IRequestHandler<AddCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ICatalog, ICatalogAggregate> _provider;
    private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    public AddCatalogItemHandler(IAggregateProvider<ICatalog, ICatalogAggregate> provider, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer, IActorProvider actor) { _provider = provider; _observer = observer; _actor = actor; }
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(AddCatalogItemCommand r, CancellationToken ct)
    {
        var id = r.Id ?? Guid.NewGuid();
        var aggregate = await _provider.GetAggregateAsync(id, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.AddItem(r.Kind, r.Name, _actor.ClientId);
    }
}
public sealed class RenameCatalogItemHandler : IRequestHandler<RenameCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ICatalog, ICatalogAggregate> _provider;
    private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
    public RenameCatalogItemHandler(IAggregateProvider<ICatalog, ICatalogAggregate> provider, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(RenameCatalogItemCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return CatalogResults.Fail("Catalog item not found.");
        return aggregate.Rename(r.Name);
    }
}
public sealed class UpdateCatalogItemDetailsHandler : IRequestHandler<UpdateCatalogItemDetailsCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ICatalog, ICatalogAggregate> _provider;
    private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
    public UpdateCatalogItemDetailsHandler(IAggregateProvider<ICatalog, ICatalogAggregate> provider, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(UpdateCatalogItemDetailsCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return CatalogResults.Fail("Catalog item not found.");
        return aggregate.UpdateDetails(r.Photo, r.Notes, r.Brand, r.FantasyPartner);
    }
}
public sealed class ArchiveCatalogItemHandler : IRequestHandler<ArchiveCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ICatalog, ICatalogAggregate> _provider;
    private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
    public ArchiveCatalogItemHandler(IAggregateProvider<ICatalog, ICatalogAggregate> provider, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(ArchiveCatalogItemCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return CatalogResults.Fail("Catalog item not found.");
        return aggregate.Archive();
    }
}
public sealed class RestoreCatalogItemHandler : IRequestHandler<RestoreCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ICatalog, ICatalogAggregate> _provider;
    private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
    public RestoreCatalogItemHandler(IAggregateProvider<ICatalog, ICatalogAggregate> provider, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(RestoreCatalogItemCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return CatalogResults.Fail("Catalog item not found.");
        return aggregate.Restore();
    }
}
public sealed class RemoveCatalogItemHandler : IRequestHandler<RemoveCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ICatalog, ICatalogAggregate> _provider;
    private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
    public RemoveCatalogItemHandler(IAggregateProvider<ICatalog, ICatalogAggregate> provider, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(RemoveCatalogItemCommand r, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return CatalogResults.Fail("Catalog item not found.");
        return aggregate.Remove();
    }
}
