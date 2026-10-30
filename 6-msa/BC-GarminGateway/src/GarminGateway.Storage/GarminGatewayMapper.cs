namespace GarminGateway.Storage;
using System.Text.Json;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;
using GarminGateway.InternalContracts;
public static class GarminGatewayMapper
{
    private static List<string> ToList(string? json) => string.IsNullOrWhiteSpace(json) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    private static string ToJson(IEnumerable<string> items) => JsonSerializer.Serialize(items.ToList());
    public static IGarminGatewayAnemicModel MapToModel(GarminSyncEntity e)
    {
        var m = new AnemicModel { Id = e.Id, Version = e.Version };
        m.Root = GarminSyncRoot.CreateInstance(e.SourceId);
        m.Credentials = Credentials.CreateInstance(e.ApiKey, e.OauthToken, e.TokenExpiry);
        m.SyncConfig = SyncConfig.CreateInstance(ToList(e.MetricsJson), e.WindowDays);
        m.LastSync = e.LastSync;
        return m;
    }
    public static void MapToEntity(IGarminGatewayAnemicModel m, GarminSyncEntity e)
    { e.Id = m.Id; e.Version = m.Version; e.SourceId = m.Root.SourceId; e.ApiKey = m.Credentials.ApiKey; e.OauthToken = m.Credentials.OauthToken; e.TokenExpiry = m.Credentials.TokenExpiry; e.MetricsJson = ToJson(m.SyncConfig.Metrics); e.WindowDays = m.SyncConfig.WindowDays; e.LastSync = m.LastSync; }
    public static GarminSyncReadModel MapToReadModel(GarminSyncEntity e) => new GarminSyncReadModel { Id = e.Id, SourceId = e.SourceId, ApiKey = e.ApiKey, OauthToken = e.OauthToken, TokenExpiry = e.TokenExpiry, Metrics = ToList(e.MetricsJson), WindowDays = e.WindowDays, LastSync = e.LastSync };
}