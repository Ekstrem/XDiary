using GarminGateway.InternalContracts;
using MediatR;

namespace GarminGateway.Application.Queries;

/// <summary>Запрос: состояние синка Garmin по источнику.</summary>
public class GetSyncStateQuery : IRequest<GarminSyncReadModel?>
{
    public string SourceId { get; init; } = "garmin";
}
