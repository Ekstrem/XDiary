using MediatR;

namespace Catalog.Application.Commands;

/// <summary>РљРѕРјР°РЅРґР°: РѕС‚СЂРµРґР°РєС‚РёСЂРѕРІР°С‚СЊ Р·Р°РїРёСЃСЊ.</summary>
public class UpdateItemCommand : IRequest<CatalogOperationResult>
{
    public Guid AggregateId { get; set; }
    public CatalogItemDraft Draft { get; set; } = new();
}
