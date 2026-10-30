namespace AppleHealthGateway.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
public sealed record ConfigureCommand(IReadOnlyList<string> Metrics) : IRequest<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>;
public sealed record ImportFileCommand(string FileName) : IRequest<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>;