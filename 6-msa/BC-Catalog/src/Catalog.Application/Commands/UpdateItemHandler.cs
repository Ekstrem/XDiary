using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.DomainServices;
using MediatR;

namespace Catalog.Application.Commands;

public class UpdateItemHandler : IRequestHandler<UpdateItemCommand, CatalogOperationResult>
{
    private readonly ICatalogAggregateProvider _provider;

    public UpdateItemHandler(ICatalogAggregateProvider provider) => _provider = provider;

    public async Task<CatalogOperationResult> Handle(UpdateItemCommand request, CancellationToken ct)
    {
        var draft = request.Draft ?? new CatalogItemDraft();

        var error = CatalogItemModelFactory.Validate(draft);
        if (error != null)
            return CatalogOperationResult.Failed(request.AggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(ICatalogItemAggregate.UpdateItem),
            "UpdateItem",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<ICatalog>.Create(
            request.AggregateId, 0, Guid.NewGuid(),
            nameof(ICatalogItemAggregate.UpdateItem), "UpdateItem",
            CatalogItemModelFactory.BuildValueObjects(draft));

        var result = aggregate.UpdateItem(model, command);
        return CatalogOperationResult.From(request.AggregateId, result);
    }
}
