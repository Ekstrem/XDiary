namespace DailyMetrics.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DailyMetrics.Domain.Abstraction;
public sealed class AnemicModel : IDailyMetricsAnemicModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid CorrelationToken { get; set; } = Guid.NewGuid();
    public IDailyMetricsRoot Root { get; set; } = null!;
    public int Stress { get; set; }
    public int RestingHr { get; set; }
    public int Hrv { get; set; }
    public ISleep Sleep { get; set; } = Implementation.Sleep.CreateInstance(0, 0, 0, 0, 0, 0);
    public IDictionary<string, IValueObject> Invariants => GetValueObjects();
    public IDictionary<string, IValueObject> GetValueObjects() => new Dictionary<string, IValueObject> { ["Root"] = Root, ["Sleep"] = Sleep };
}