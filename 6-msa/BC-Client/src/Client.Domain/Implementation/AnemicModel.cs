namespace Client.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Client.Domain.Abstraction;
// Канон EDM §8.1: иммутабельный агрегат — бизнес-метод порождает НОВУЮ версию,
// поэтому поля init-only, а не set (мутация через `with`, не через сеттеры).
internal sealed record AnemicModel : IClientAnemicModel
{
    public static IClientAnemicModel Create(Guid id) => new AnemicModel { Id = id };

    public Guid Id { get; init; } = Guid.NewGuid();
    public long Version { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid CorrelationToken { get; init; } = Guid.NewGuid();
    public IClientRoot Root { get; init; } = null!;
    public string OtpCode { get; init; } = string.Empty;
    public DateTime? OtpExpiresAt { get; init; }
    public int OtpAttempts { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Gender { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public bool Verified { get; init; }
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root };
}
