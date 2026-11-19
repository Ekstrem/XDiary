namespace DailyMetrics.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain.Abstraction;

// Канон EDM §8.1: иммутабельный агрегат — бизнес-метод порождает НОВУЮ версию,
// поэтому поля init-only, а не set (мутация через `with`, не через сеттеры).
internal sealed record AnemicModel : IDailyMetricsAnemicModel
{
    public static IDailyMetricsAnemicModel Create(Guid id) => new AnemicModel { Id = id };
    public Guid Id { get; init; } = Guid.NewGuid();
    public long Version { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid CorrelationToken { get; init; } = Guid.NewGuid();
    public IDailyMetricsRoot Root { get; init; } = null!;
    public int Stress { get; init; }
    public int RestingHr { get; init; }
    public int Hrv { get; init; }
    public ISleep Sleep { get; init; } = Implementation.Sleep.CreateInstance(0, 0, 0, 0, 0, 0);
    public Guid? ClientId { get; init; }
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject>
    {
        ["Root"] = Root,
        ["Stress"] = new IntValueObject(Stress),
        ["RestingHr"] = new IntValueObject(RestingHr),
        ["Hrv"] = new IntValueObject(Hrv),
        ["Sleep"] = Sleep,
        ["ClientId"] = new ClientIdValueObject(ClientId),
    };
}
