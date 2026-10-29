using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;

namespace Privacy.InternalContracts;

/// <summary>
/// Материализованное представление учётных данных PIN (проекция в Read Store).
/// Хранит только соль + хеш — не открытый PIN.
/// </summary>
public class PrivacyReadModel : IReadModel<IPrivacy>
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public int Iterations { get; set; }

    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
