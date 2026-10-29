using MediatR;

namespace Catalog.Application.Commands;

/// <summary>РљРѕРјР°РЅРґР°: СЃРѕР·РґР°С‚СЊ Р·Р°РїРёСЃСЊ РёР· РІРёР·Р°СЂРґР°.</summary>
public class CreateItemCommand : IRequest<CatalogOperationResult>
{
    public CatalogItemDraft Draft { get; set; } = new();
}
