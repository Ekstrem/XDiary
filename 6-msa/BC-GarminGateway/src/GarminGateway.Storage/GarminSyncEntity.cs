namespace GarminGateway.Storage;
public sealed class GarminSyncEntity
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string OauthToken { get; set; } = string.Empty;
    public string TokenExpiry { get; set; } = string.Empty;
    public string MetricsJson { get; set; } = "[]";
    public int WindowDays { get; set; }
    public string LastSync { get; set; } = string.Empty;
    public long Version { get; set; }
}