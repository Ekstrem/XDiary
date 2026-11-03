namespace Partners.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

public sealed record DeletedAtValueObject(DateTime? Value) : IValueObject;
