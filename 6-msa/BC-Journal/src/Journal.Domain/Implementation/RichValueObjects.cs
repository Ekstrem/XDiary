namespace Journal.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain.Abstraction;

// РљР°РЅРѕРЅ EDM, РєР°РЅРІР°СЃ В§2.3: Р±РѕРіР°С‚С‹Рµ VO вЂ” РёРјРјСѓС‚Р°Р±РµР»СЊРЅС‹Рµ value objects (record), С„Р°Р±СЂРёРєР° Create.
// Р’ РѕС‚Р»РёС‡РёРµ РѕС‚ СЃРІС‘СЂС‚РєРё СЃРєР°Р»СЏСЂРѕРІ (StringValueObject/IntValueObject), РіСЂСѓРїРїРёСЂСѓСЋС‚ Р°С‚СЂРёР±СѓС‚С‹
// РїРѕ СЃРјС‹СЃР»Сѓ Рё РіРѕРІРѕСЂСЏС‚ РЅР° ubiquitous language. When вЂ” СЃРёР»СЊРЅР°СЏ С‚РёРїРёР·Р°С†РёСЏ (DateTimeOffset).
internal sealed record When(DateTimeOffset StartedAt) : IValueObject
{
    public static When Create(DateTimeOffset startedAt) => new(startedAt);
}

internal sealed record PlaceValue(string Value) : IValueObject
{
    public static PlaceValue Create(string value) => new(value);
}

internal sealed record PositionsValue(IReadOnlyList<string> Value) : IValueObject
{
    public static PositionsValue Create(IEnumerable<string> value) => new(value.ToList());
}

internal sealed record AccessoriesValue(IReadOnlyList<string> Costumes, IReadOnlyList<string> Toys, IReadOnlyList<string> Fantasies, IReadOnlyList<string> Tags) : IValueObject
{
    public static AccessoriesValue Create(IEnumerable<string> costumes, IEnumerable<string> toys, IEnumerable<string> fantasies, IEnumerable<string> tags)
        => new(costumes.ToList(), toys.ToList(), fantasies.ToList(), tags.ToList());
}

internal sealed record ContraceptionValue(IReadOnlyList<string> Contraceptives, int ContraceptiveCount) : IValueObject
{
    public static ContraceptionValue Create(IEnumerable<string> contraceptives, int count) => new(contraceptives.ToList(), count);
}

internal sealed record DurationValue(int Minutes) : IValueObject
{
    public static DurationValue Create(int minutes) => new(minutes);
}

internal sealed record OriginValue(bool Garmin, string GarminId) : IValueObject
{
    public static OriginValue Create(bool garmin, string garminId) => new(garmin, garminId);
}
