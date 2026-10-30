namespace SomaticSeries.Application.Commands;
using DigiTFactory.Libraries.SeedWorks.Result;
using MediatR;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
public sealed record RecordPointCommand(string T, int Hr, int BodyBattery) : IRequest<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>>;