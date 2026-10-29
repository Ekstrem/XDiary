using MediatR;

namespace Partners.Application.Commands;

/// <summary>РљРѕРјР°РЅРґР°: СѓРґР°Р»РёС‚СЊ Р·Р°РїРёСЃСЊ.</summary>
public class DeletePartnerCommand : IRequest<PartnersOperationResult>
{
    public Guid AggregateId { get; set; }
}
