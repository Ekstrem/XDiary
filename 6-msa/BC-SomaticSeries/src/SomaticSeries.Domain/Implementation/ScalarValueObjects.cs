namespace SomaticSeries.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
internal sealed record IntValueObject(int Value) : IValueObject;
internal sealed record ClientIdValueObject(Guid? Value) : IValueObject;
