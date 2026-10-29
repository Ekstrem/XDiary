using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.DomainServices;
using MediatR;

namespace Catalog.Application.Commands;

public class DeleteItemHandler : IRequestHandler<DeleteItemCommand, CatalogOperationResult>
{
    private readonly ICatalogAggregateProvider _provider;

    public DeleteItemHandler(ICatalogAggregateProvider provider) => _provider = provider;

    public async Task<CatalogOperationResult> Handle(DeleteItemCommand request, CancellationToken ct)
    {
        var aggregate = await _provider.GetAggregateAsync(request.AggregateId, ct);

        var command = CommandToAggregate.Commit(
            Guid.NewGuid(),
            nameof(ICatalogItemAggregate.DeleteItem),
            "DeleteItem",
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        var model = DynamicAnemicModel<ICatalog>.Create(
            request.AggregateId, 0, Guid.NewGuid(),
            nameof(ICatalogItemAggregate.DeleteItem), "DeleteItem",
            new Dictionary<string, IValueObject>());

        var result = aggregate.DeleteItem(model, command);
        return CatalogOperationResult.From(request.AggregateId, result);
    }
}
