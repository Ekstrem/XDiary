using MediatR;

namespace GarminGateway.Application.Commands;

/// <summary>Команда: сохранить состояние синка Garmin (upsert по sourceId).</summary>
public class UpsertSyncStateCommand : IRequest<GarminGatewayOperationResult>
{
    public string SourceId { get; set; } = "garmin";
    public string ApiKey { get; set; } = string.Empty;
    public string OauthToken { get; set; } = string.Empty;
    public string TokenExpiry { get; set; } = string.Empty;
    public List<string> Metrics { get; set; } = new();
    public int WindowDays { get; set; }
    public string LastSync { get; set; } = string.Empty;
}
