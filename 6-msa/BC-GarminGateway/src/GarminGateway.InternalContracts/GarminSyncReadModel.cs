using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;

namespace GarminGateway.InternalContracts;

/// <summary>
/// Материализованное представление состояния синка Garmin (проекция в Read Store).
/// </summary>
public class GarminSyncReadModel : IReadModel<IGarminGateway>
{
    public Guid Id { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string OauthToken { get; set; } = string.Empty;
    public string TokenExpiry { get; set; } = string.Empty;
    public List<string> Metrics { get; set; } = new();
    public int WindowDays { get; set; }
    public string LastSync { get; set; } = string.Empty;

    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
