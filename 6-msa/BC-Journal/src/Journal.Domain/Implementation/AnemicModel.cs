namespace Journal.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain.Abstraction;

public sealed class AnemicModel : IJournalAnemicModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Version { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public Guid CorrelationToken { get; set; } = Guid.NewGuid();

    public IRecordRoot Root { get; set; } = null!;
    public int DurationMin { get; set; }
    public string Place { get; set; } = string.Empty;
    public IReadOnlyList<string> Positions { get; set; } = new List<string>();
    public IReadOnlyList<string> Costumes { get; set; } = new List<string>();
    public IReadOnlyList<string> Toys { get; set; } = new List<string>();
    public IReadOnlyList<string> Fantasies { get; set; } = new List<string>();
    public IReadOnlyList<string> Tags { get; set; } = new List<string>();
    public IReadOnlyList<string> Contraceptives { get; set; } = new List<string>();
    public int ContraceptiveCount { get; set; }
    public IReadOnlyList<IOrgasms> Orgasms { get; set; } = new List<IOrgasms>();
    public bool Garmin { get; set; }
    public string GarminId { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;

    public IDictionary<string, IValueObject> Invariants => GetValueObjects();

    public IDictionary<string, IValueObject> GetValueObjects()
        => new Dictionary<string, IValueObject> { ["Root"] = Root };
}
