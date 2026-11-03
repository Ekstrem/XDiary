namespace Partners.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

public sealed record ClientIdValueObject(Guid? Value) : IValueObject;
