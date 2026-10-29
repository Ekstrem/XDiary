using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.DomainServices;
using MediatR;

namespace Catalog.Application.Commands;

public class CreateItemHandler : IRequestHandler<CreateItemCommand, CatalogOperationResult>
{
    private readonly ICatalogAggregateProvider _provider;

    public CreateItemHandler(ICatalogAggregateProvider provider) => _provider = provider;

    public async Task<CatalogOperationResult> Handle(CreateItemCommand request, CancellationToken ct)
    {
        var draft = request.Draft ?? new CatalogItemDraft();
        var aggregateId = Guid.NewGuid();

        var error = CatalogItemModelFactory.Validate(draft);
        if (error != null)
            return CatalogOperationResult.Failed(aggregateId, error);

        var aggregate = await _provider.GetAggregateAsync(aggregateId, ct);
        var model = DynamicAnemicModel<ICatalog>.Create(
            aggregateId, 0, Guid.NewGuid(),
            nameof(ICatalogItemAggregate.CreateItem), "CreateItem",
            CatalogItemModelFactory.BuildValueObjects(draft));

        var result = aggregate.CreateItem(model);
        return CatalogOperationResult.From(aggregateId, result);
    }
}
