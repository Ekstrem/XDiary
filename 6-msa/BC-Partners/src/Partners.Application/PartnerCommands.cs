namespace Partners.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Partners.Domain;
using Partners.Domain.Abstraction;
public sealed record RegisterPartnerCommand(string Name) : IRequest<AggregateResult<IPartners, IPartnerAnemicModel>>;
public sealed record RenamePartnerCommand(Guid AggregateId, string Name) : IRequest<AggregateResult<IPartners, IPartnerAnemicModel>>;
public sealed record UpdatePartnerProfileCommand(Guid AggregateId, string Photo, string Gender, string Notes) : IRequest<AggregateResult<IPartners, IPartnerAnemicModel>>;
public sealed record RemovePartnerCommand(Guid AggregateId) : IRequest<AggregateResult<IPartners, IPartnerAnemicModel>>;