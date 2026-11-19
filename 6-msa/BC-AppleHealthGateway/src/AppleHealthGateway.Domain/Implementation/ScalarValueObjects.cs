namespace AppleHealthGateway.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
internal sealed record StringValueObject(string Value) : IValueObject;
internal sealed record StringListValueObject(IReadOnlyList<string> Value) : IValueObject;
