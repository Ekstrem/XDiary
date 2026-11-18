namespace Client.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
internal sealed record StringValueObject(string Value) : IValueObject;
internal sealed record NullableStringValueObject(string? Value) : IValueObject;
internal sealed record IntValueObject(int Value) : IValueObject;
internal sealed record BoolValueObject(bool Value) : IValueObject;
internal sealed record DateTimeValueObject(DateTime? Value) : IValueObject;
