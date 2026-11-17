namespace Journal.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain.Abstraction;

/// <summary>РћР±С‘СЂС‚РєРё СЃРєР°Р»СЏСЂРѕРІ РґР»СЏ change-track (E1 В§1.2 вЂ” defaults РІ MV).</summary>
internal sealed record StringValueObject(string Value) : IValueObject;
internal sealed record StringListValueObject(IReadOnlyList<string> Value) : IValueObject;
internal sealed record IntValueObject(int Value) : IValueObject;
internal sealed record BoolValueObject(bool Value) : IValueObject;
// BE-Рєanon В§15: РїРѕР»РЅРѕС‚Р° diff вЂ” РѕСЂРіР°Р·РјС‹, garmin Рё count РѕР±СЏР·Р°РЅС‹ РїРѕРїР°РґР°С‚СЊ РІ change-track,
// РёРЅР°С‡Рµ MV РЅРµ РїРѕР»СѓС‡Р°РµС‚ РёС… РІРѕРІСЃРµ.
internal sealed record OrgasmsListValueObject(IReadOnlyList<IOrgasms> Value) : IValueObject;
