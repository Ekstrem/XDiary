namespace Journal.Domain.Specifications;

using System.Globalization;

/// <summary>
/// TE-101: startedAt РѕР±СЏР·Р°РЅ Р±С‹С‚СЊ РґР°С‚РѕР№ РІ С„РѕСЂРјР°С‚Рµ ISO 8601. РћС‚РІРµСЂРіР°РµС‚ РїСѓСЃС‚СѓСЋ СЃС‚СЂРѕРєСѓ,
/// В«РЅРµ РґР°С‚Р°В» Рё С‡РёСЃС‚С‹Рµ С‡РёСЃР»Р° (В«1234567890В»); РїСЂРё СѓСЃРїРµС…Рµ РѕС‚РґР°С‘С‚ **DateTimeOffset** (UTC),
/// С‡С‚РѕР±С‹ РјСѓСЃРѕСЂ РЅРµ РїРѕРїР°РґР°Р» РІ СЃРѕСЂС‚РёСЂРѕРІРєСѓ/С‚СЂРµРЅРґС‹/CSV.
/// </summary>
internal sealed class WhenIsoParser
{
    public bool TryParse(string startedAt, out DateTimeOffset normalized)
    {
        var t = (startedAt ?? "").Trim();
        if (t.Length > 0
            && DateTimeOffset.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
        {
            // TE-101: РєР°РЅРѕРЅРёР·РёСЂСѓРµРј Рє UTC, С‡С‚РѕР±С‹ СЂР°РІРЅС‹Рµ РјРѕРјРµРЅС‚С‹ РґР°РІР°Р»Рё РћР”РќРћ Р·РЅР°С‡РµРЅРёРµ.
            normalized = dt.ToUniversalTime();
            return true;
        }
        normalized = default;
        return false;
    }

    public string Reason => "startedAt РґРѕР»Р¶РµРЅ Р±С‹С‚СЊ РґР°С‚РѕР№ РІ С„РѕСЂРјР°С‚Рµ ISO 8601.";
}
