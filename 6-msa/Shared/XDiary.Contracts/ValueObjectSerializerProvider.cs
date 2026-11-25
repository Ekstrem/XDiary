namespace XDiary.Contracts;

using System.Text.Json;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>
/// BE-152: единая точка создания библиотечного IValueObjectSerializer (QG-309/311).
/// Legacy-формат {$type,value} читается fallback'ом, чтобы старые события не ломались после апгрейда.
/// </summary>
public static class ValueObjectSerializerProvider
{
    public static IValueObjectSerializer Create(Action<JsonSerializerOptions>? configure = null)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        configure?.Invoke(options);
        return new JsonEnvelopeValueObjectSerializer(new ValueObjectTypeRegistry(), options);
    }

    public static IReadOnlyDictionary<string, IValueObject> DeserializeWithLegacy(
        IValueObjectSerializer serializer,
        string payload,
        Action<JsonSerializerOptions>? configureLegacy = null)
    {
        try
        {
            return serializer.Deserialize(payload);
        }
        catch (Exception)
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new IValueObjectJsonConverter());
            configureLegacy?.Invoke(options);
            return JsonSerializer.Deserialize<IDictionary<string, IValueObject>>(payload, options)!
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}
