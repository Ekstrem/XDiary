namespace Partners.Storage.Outbox;
using System.Text.Json;
using System.Text.Json.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>
/// BE-059: полиморфная сериализация IValueObject для outbox-Payload.
/// Пишет {$type, value}; читает по AssemblyQualifiedName. Concrete-типы (record/VO)
/// должны быть десериализуемы (JsonConstructor / positional record).
/// </summary>
public sealed class IValueObjectJsonConverter : JsonConverter<IValueObject>
{
    public override IValueObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var typeName = root.GetProperty("$type").GetString();
        var type = Type.GetType(typeName!)
            ?? throw new InvalidOperationException($"тип VO не найден: {typeName}");
        return (IValueObject)JsonSerializer.Deserialize(root.GetProperty("value").GetRawText(), type, options)!;
    }

    public override void Write(Utf8JsonWriter writer, IValueObject value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("$type", value.GetType().AssemblyQualifiedName);
        writer.WritePropertyName("value");
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
        writer.WriteEndObject();
    }
}
