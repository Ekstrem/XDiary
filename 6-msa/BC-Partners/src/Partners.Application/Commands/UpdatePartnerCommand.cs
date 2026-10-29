using MediatR;

namespace Partners.Application.Commands;

/// <summary>РљРѕРјР°РЅРґР°: РѕС‚СЂРµРґР°РєС‚РёСЂРѕРІР°С‚СЊ Р·Р°РїРёСЃСЊ.</summary>
public class UpdatePartnerCommand : IRequest<PartnersOperationResult>
{
    public Guid AggregateId { get; set; }
    public PartnerDraft Draft { get; set; } = new();
}
