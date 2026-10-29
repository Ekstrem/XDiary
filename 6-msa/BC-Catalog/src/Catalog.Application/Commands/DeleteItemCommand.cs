using MediatR;

namespace Catalog.Application.Commands;

/// <summary>РљРѕРјР°РЅРґР°: СѓРґР°Р»РёС‚СЊ Р·Р°РїРёСЃСЊ.</summary>
public class DeleteItemCommand : IRequest<CatalogOperationResult>
{
    public Guid AggregateId { get; set; }
}
