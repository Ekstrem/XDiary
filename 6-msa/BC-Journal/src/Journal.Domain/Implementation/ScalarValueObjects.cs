namespace Journal.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain.Abstraction;

/// <summary>Обёртки скаляров для change-track (E1 §1.2 — defaults в MV).</summary>
public sealed record StringValueObject(string Value) : IValueObject;
public sealed record StringListValueObject(IReadOnlyList<string> Value) : IValueObject;
public sealed record IntValueObject(int Value) : IValueObject;
public sealed record BoolValueObject(bool Value) : IValueObject;
// BE-кanon §15: полнота diff — оргазмы, garmin и count обязаны попадать в change-track,
// иначе MV не получает их вовсе.
public sealed record OrgasmsListValueObject(IReadOnlyList<IOrgasms> Value) : IValueObject;
