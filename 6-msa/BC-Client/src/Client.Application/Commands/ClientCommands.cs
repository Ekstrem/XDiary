namespace Client.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using Client.Domain;
using Client.Domain.Abstraction;
public sealed record RegisterClientCommand(string Phone) : IRequest<AggregateResult<IClients, IClientAnemicModel>>;
public sealed record VerifyClientOtpCommand(Guid AggregateId, string Code) : IRequest<AggregateResult<IClients, IClientAnemicModel>>;
public sealed record UpdateProfileCommand(Guid AggregateId, string DisplayName, string Gender, string? PhotoUrl) : IRequest<AggregateResult<IClients, IClientAnemicModel>>;