namespace Journal.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain.Abstraction;

// Канон EDM, канвас §2.3: богатые VO — иммутабельные value objects (record), фабрика Create.
// В отличие от свёртки скаляров (StringValueObject/IntValueObject), группируют атрибуты
// по смыслу и говорят на ubiquitous language. When — сильная типизация (DateTimeOffset).
public sealed record When(DateTimeOffset StartedAt) : IValueObject
{
    public static When Create(DateTimeOffset startedAt) => new(startedAt);
}

public sealed record PlaceValue(string Value) : IValueObject
{
    public static PlaceValue Create(string value) => new(value);
}

public sealed record PositionsValue(IReadOnlyList<string> Value) : IValueObject
{
    public static PositionsValue Create(IEnumerable<string> value) => new(value.ToList());
}

public sealed record AccessoriesValue(IReadOnlyList<string> Costumes, IReadOnlyList<string> Toys, IReadOnlyList<string> Fantasies, IReadOnlyList<string> Tags) : IValueObject
{
    public static AccessoriesValue Create(IEnumerable<string> costumes, IEnumerable<string> toys, IEnumerable<string> fantasies, IEnumerable<string> tags)
        => new(costumes.ToList(), toys.ToList(), fantasies.ToList(), tags.ToList());
}

public sealed record ContraceptionValue(IReadOnlyList<string> Contraceptives, int ContraceptiveCount) : IValueObject
{
    public static ContraceptionValue Create(IEnumerable<string> contraceptives, int count) => new(contraceptives.ToList(), count);
}

public sealed record DurationValue(int Minutes) : IValueObject
{
    public static DurationValue Create(int minutes) => new(minutes);
}

public sealed record OriginValue(bool Garmin, string GarminId) : IValueObject
{
    public static OriginValue Create(bool garmin, string garminId) => new(garmin, garminId);
}
