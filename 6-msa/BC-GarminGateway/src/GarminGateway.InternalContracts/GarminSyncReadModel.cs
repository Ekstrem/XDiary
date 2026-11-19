using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
namespace GarminGateway.InternalContracts;
using XDiary.Contracts;
public sealed class GarminSyncReadModel : IReadModel<GarminGateway.Domain.IGarminGateway>
{
    public Guid Id { get; set; }
    // BE-140: версия события для stale-skip (QG-305).
    public long Version { get; set; }
    public Guid ClientId { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string OauthToken { get; set; } = string.Empty;
    public string TokenExpiry { get; set; } = string.Empty;
    public IReadOnlyList<string> Metrics { get; set; } = new List<string>();
    public int WindowDays { get; set; }
    public string LastSync { get; set; } = string.Empty;
}