namespace Partners.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>РћР±С‘СЂС‚РєРё СЃРєР°Р»СЏСЂРѕРІ РґР»СЏ change-track (TE-102: Photo/Gender/Notes РґРѕР»Р¶РЅС‹ РїРѕРїР°РґР°С‚СЊ РІ ChangedValueObjects).</summary>
internal sealed record StringValueObject(string Value) : IValueObject;
