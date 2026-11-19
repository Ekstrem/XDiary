using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
namespace Privacy.InternalContracts;
using XDiary.Contracts;
public sealed class PinCredentialReadModel : IReadModel<Privacy.Domain.IPrivacy>
{
    public Guid Id { get; set; }
    // BE-140: версия события для stale-skip (QG-305).
    public long Version { get; set; }
    public Guid ClientId { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public int Iterations { get; set; }
    public bool Locked { get; set; }
    public bool HasPin => !string.IsNullOrEmpty(Hash);
}