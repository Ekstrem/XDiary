namespace Journal.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain.Abstraction;

// Канон EDM §8.1: иммутабельный агрегат — бизнес-метод порождает НОВУЮ версию,
// поэтому поля init-only, а не set (мутация через `with`, не через сеттеры).
public sealed record AnemicModel : IJournalAnemicModel
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public long Version { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public string CommandName { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public Guid CorrelationToken { get; init; } = Guid.NewGuid();

    public IRecordRoot Root { get; init; } = null!;
    public Guid? ClientId { get; init; }
    public int DurationMin { get; init; }
    public string Place { get; init; } = string.Empty;
    public IReadOnlyList<string> Positions { get; init; } = new List<string>();
    public IReadOnlyList<string> Costumes { get; init; } = new List<string>();
    public IReadOnlyList<string> Toys { get; init; } = new List<string>();
    public IReadOnlyList<string> Fantasies { get; init; } = new List<string>();
    public IReadOnlyList<string> Tags { get; init; } = new List<string>();
    public IReadOnlyList<string> Contraceptives { get; init; } = new List<string>();
    public int ContraceptiveCount { get; init; }
    public IReadOnlyList<IOrgasms> Orgasms { get; init; } = new List<IOrgasms>();
    public bool Garmin { get; init; }
    public string GarminId { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
    public RecordState State { get; init; } = RecordState.Draft;
    public bool Deleted { get; init; }

    public IDictionary<string, IValueObject> Invariants => GetValueObjects();

    public IDictionary<string, IValueObject> GetValueObjects()
        => new Dictionary<string, IValueObject>
        {
            ["CommandName"] = new StringValueObject(CommandName),
            ["Root"] = Root,
            ["ClientId"] = new ClientIdValueObject(ClientId),
            ["Place"] = PlaceValue.Create(Place),
            ["Positions"] = PositionsValue.Create(Positions),
            ["Accessories"] = AccessoriesValue.Create(Costumes, Toys, Fantasies, Tags),
            ["Contraception"] = ContraceptionValue.Create(Contraceptives, ContraceptiveCount),
            ["Duration"] = DurationValue.Create(DurationMin),
            ["Orgasms"] = new OrgasmsListValueObject(Orgasms),
            ["Origin"] = OriginValue.Create(Garmin, GarminId),
            ["State"] = new IntValueObject((int)State),
            ["Deleted"] = new BoolValueObject(Deleted),
        };
}
