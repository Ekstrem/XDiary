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
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<ICatalog, ICatalogAggregate>)_provider).GetAggregateWithCompletionAsync(id, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        var result = aggregate.AddItem(r.Kind, r.Name, _actor.ClientId);
        await completion;
        return result;
    }
}
public sealed class RenameCatalogItemHandler : IRequestHandler<RenameCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ICatalog, ICatalogAggregate> _provider;
    private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
    public RenameCatalogItemHandler(IAggregateProvider<ICatalog, ICatalogAggregate> provider, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(RenameCatalogItemCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<ICatalog, ICatalogAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return CatalogResults.Fail("Catalog item not found.");
        var result = aggregate.Rename(r.Name);
        await completion;
        return result;
    }
}
public sealed class UpdateCatalogItemDetailsHandler : IRequestHandler<UpdateCatalogItemDetailsCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ICatalog, ICatalogAggregate> _provider;
    private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
    public UpdateCatalogItemDetailsHandler(IAggregateProvider<ICatalog, ICatalogAggregate> provider, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(UpdateCatalogItemDetailsCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<ICatalog, ICatalogAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return CatalogResults.Fail("Catalog item not found.");
        var result = aggregate.UpdateDetails(r.Photo, r.Notes, r.Brand, r.FantasyPartner);
        await completion;
        return result;
    }
}
public sealed class ArchiveCatalogItemHandler : IRequestHandler<ArchiveCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ICatalog, ICatalogAggregate> _provider;
    private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
    public ArchiveCatalogItemHandler(IAggregateProvider<ICatalog, ICatalogAggregate> provider, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(ArchiveCatalogItemCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<ICatalog, ICatalogAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return CatalogResults.Fail("Catalog item not found.");
        var result = aggregate.Archive();
        await completion;
        return result;
    }
}
public sealed class RestoreCatalogItemHandler : IRequestHandler<RestoreCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ICatalog, ICatalogAggregate> _provider;
    private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
    public RestoreCatalogItemHandler(IAggregateProvider<ICatalog, ICatalogAggregate> provider, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(RestoreCatalogItemCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<ICatalog, ICatalogAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return CatalogResults.Fail("Catalog item not found.");
        var result = aggregate.Restore();
        await completion;
        return result;
    }
}
public sealed class RemoveCatalogItemHandler : IRequestHandler<RemoveCatalogItemCommand, AggregateResult<ICatalog, ICatalogAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ICatalog, ICatalogAggregate> _provider;
    private readonly IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> _observer;
    public RemoveCatalogItemHandler(IAggregateProvider<ICatalog, ICatalogAggregate> provider, IObserver<AggregateResult<ICatalog, ICatalogAnemicModel>> observer) { _provider = provider; _observer = observer; }
    public async Task<AggregateResult<ICatalog, ICatalogAnemicModel>> Handle(RemoveCatalogItemCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<ICatalog, ICatalogAggregate>)_provider).GetAggregateWithCompletionAsync(r.AggregateId, ct);
        ((IObservable<AggregateResult<ICatalog, ICatalogAnemicModel>>)aggregate).Subscribe(_observer);
        if (aggregate.Root is null) return CatalogResults.Fail("Catalog item not found.");
        var result = aggregate.Remove();
        await completion;
        return result;
    }
}
