namespace Partners.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

internal sealed record DeletedAtValueObject(DateTime? Value) : IValueObject;
