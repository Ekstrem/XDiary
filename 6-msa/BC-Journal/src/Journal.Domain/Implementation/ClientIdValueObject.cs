namespace Journal.Domain.Implementation;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>
/// VO-РѕР±С‘СЂС‚РєР° СЃРєР°Р»СЏСЂР° ClientId, С‡С‚РѕР±С‹ РѕРЅ РїРѕРїР°РґР°Р» РІ change-track `GetValueObjects()`
/// Рё, РєР°Рє СЃР»РµРґСЃС‚РІРёРµ, РІ РєРѕРЅРІРµСЂС‚ СЃРѕР±С‹С‚РёСЏ (clientId РѕР±СЏР·Р°С‚РµР»РµРЅ РїРѕ ADR-028 / event-delivery).
/// </summary>
internal sealed record ClientIdValueObject(Guid? Value) : IValueObject;
