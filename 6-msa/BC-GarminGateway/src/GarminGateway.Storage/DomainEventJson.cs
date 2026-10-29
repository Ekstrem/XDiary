using System.Text.Json;
using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

namespace GarminGateway.Storage;

/// <summary>
/// РЎРµСЂРёР°Р»РёР·Р°С†РёСЏ ChangedValueObjects (СЃР»РѕРІР°СЂСЊ VO) РІ/РёР· JSON.
/// РҐСЂР°РЅРёС‚ runtime-С‚РёРї VO Рё СЃРѕС…СЂР°РЅСЏРµС‚ string[] РєР°Рє РјР°СЃСЃРёРІС‹.
/// </summary>
internal static class DomainEventJson
{
    public static string Serialize(IDictionary<string, IValueObject> vos)
    {
        var payload = new Dictionary<string, object?>();
        foreach (var (key, vo) in vos)
        {
            if (vo is DynamicValueObject dvo)
                payload[key] = new Dictionary<string, object?>
                {
                    ["TypeName"] = dvo.TypeName,
                    ["Properties"] = dvo.Properties
                };
        }
        return JsonSerializer.Serialize(payload);
    }

    public static IDictionary<string, IValueObject> Deserialize(string? json)
    {
        var result = new Dictionary<string, IValueObject>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            result[prop.Name] = ReadValueObject(prop.Value);
        }
        return result;
    }

    private static DynamicValueObject ReadValueObject(JsonElement el)
    {
        var typeName = el.TryGetProperty("TypeName", out var tn) ? tn.GetString() ?? "Unknown" : "Unknown";
        var props = new Dictionary<string, object?>();

        if (el.TryGetProperty("Properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in propsEl.EnumerateObject())
                props[p.Name] = ReadJsonValue(p.Value);
        }

        return new DynamicValueObject(typeName, props);
    }

    private static object? ReadJsonValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt32(out var i) ? i : el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => el.EnumerateArray().Select(e => e.GetString()).ToArray(),
        _ => el.GetRawText()
    };
}
