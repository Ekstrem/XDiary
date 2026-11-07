namespace Partners.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>Обёртки скаляров для change-track (TE-102: Photo/Gender/Notes должны попадать в ChangedValueObjects).</summary>
public sealed record StringValueObject(string Value) : IValueObject;
