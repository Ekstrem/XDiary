using MediatR;

namespace Partners.Application.Commands;

/// <summary>РљРѕРјР°РЅРґР°: СЃРѕР·РґР°С‚СЊ Р·Р°РїРёСЃСЊ РёР· РІРёР·Р°СЂРґР°.</summary>
public class CreatePartnerCommand : IRequest<PartnersOperationResult>
{
    public PartnerDraft Draft { get; set; } = new();
}
