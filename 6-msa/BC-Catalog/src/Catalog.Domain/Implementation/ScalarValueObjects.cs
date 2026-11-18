namespace Catalog.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
internal sealed record StringValueObject(string Value) : IValueObject;
internal sealed record ClientIdValueObject(Guid? Value) : IValueObject;
internal sealed record BoolValueObject(bool Value) : IValueObject;
