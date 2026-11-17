namespace Privacy.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain.Abstraction;

// QG006: иммутабельная анемичная модель — init-only свойства, а не set
// (мутация порождает новую версию через `with`, не через сеттеры).
internal sealed record AnemicModel : IPinCredentialAnemicModel
{
    public static IPinCredentialAnemicModel Create(Guid id) => new AnemicModel { Id = id };
    public Guid Id { get; init; } = Guid.NewGuid();
    public long Version { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid CorrelationToken { get; init; } = Guid.NewGuid();
    public IPinCredentialRoot Root { get; init; } = PinCredentialRoot.CreateInstance(Guid.Empty, "", "", 0);
    public bool Locked { get; init; }

    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root };
}
