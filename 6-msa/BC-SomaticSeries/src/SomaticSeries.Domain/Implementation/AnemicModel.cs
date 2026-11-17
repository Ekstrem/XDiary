namespace SomaticSeries.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using SomaticSeries.Domain.Abstraction;

// QG006: иммутабельная анемичная модель — init-only свойства, а не set
// (мутация порождает новую версию через `with`, не через сеттеры).
internal sealed record AnemicModel : ISomaticSeriesAnemicModel
{
    public static ISomaticSeriesAnemicModel Create(Guid id) => new AnemicModel { Id = id };
    public Guid Id { get; init; } = Guid.NewGuid();
    public long Version { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid CorrelationToken { get; init; } = Guid.NewGuid();
    public ISomaticPointRoot Root { get; init; } = null!;
    public int Hr { get; init; }
    public int BodyBattery { get; init; }
    public Guid? ClientId { get; init; }

    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root };
}
