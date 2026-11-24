namespace Journal.Domain.Implementation;

using System.Text.Json;
using System.Text.Json.Serialization;
using Journal.Domain.Abstraction;

/// <summary>BE-151: десериализация IOrgasms в OrgasmsListValueObject (event store replay).</summary>
internal sealed class OrgasmsJsonConverter : JsonConverter<IOrgasms>
{
    public override IOrgasms Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var participant = root.GetProperty("Participant").GetString()!;
        var count = root.GetProperty("Count").GetInt32();
        var rating = root.GetProperty("Rating").GetInt32();
        var types = root.GetProperty("Types").EnumerateArray().Select(x => x.GetString()!).ToArray();
        return Orgasms.CreateInstance(participant, count, types, rating);
    }

    public override void Write(Utf8JsonWriter writer, IOrgasms value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Participant", value.Participant);
        writer.WriteNumber("Count", value.Count);
        writer.WritePropertyName("Types");
        JsonSerializer.Serialize(writer, value.Types, options);
        writer.WriteNumber("Rating", value.Rating);
        writer.WriteEndObject();
    }
}
