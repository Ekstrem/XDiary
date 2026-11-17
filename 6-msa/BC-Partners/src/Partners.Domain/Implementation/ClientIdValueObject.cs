namespace Partners.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

internal sealed record ClientIdValueObject(Guid? Value) : IValueObject;
