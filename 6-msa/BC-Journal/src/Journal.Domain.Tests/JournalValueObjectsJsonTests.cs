using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain.Implementation;
using XDiary.Contracts;
using Xunit;

// BE-151 (DS JournalValueObjectsJsonTests): полиморфная сериализация VO для event store.
public class JournalValueObjectsJsonTests
{
    private static readonly IValueObjectSerializer Serializer = new JsonEnvelopeValueObjectSerializer(new ValueObjectTypeRegistry(), new System.Text.Json.JsonSerializerOptions { Converters = { new OrgasmsJsonConverter() } });

    private static T RoundTrip<T>(T value) where T : IValueObject
    {
        var json = Serializer.Serialize(new Dictionary<string, IValueObject> { ["value"] = value });
        return (T)Serializer.Deserialize(json)["value"];
    }

    [Fact]
    public void PlaceValue_When_RoundTripped_Should_PreserveValue()
        => Assert.Equal("дома", RoundTrip(PlaceValue.Create("дома")).Value);

    [Fact]
    public void PositionsValue_When_RoundTripped_Should_PreserveList()
        => Assert.Equal(new[] { "p1", "p2" }, RoundTrip(PositionsValue.Create(new[] { "p1", "p2" })).Value);

    [Fact]
    public void AccessoriesValue_When_RoundTripped_Should_PreserveAllGroups()
    {
        var value = RoundTrip(AccessoriesValue.Create(new[] { "c" }, new[] { "t" }, new[] { "f" }, new[] { "tag" }));
        Assert.Equal(new[] { "c" }, value.Costumes);
        Assert.Equal(new[] { "t" }, value.Toys);
        Assert.Equal(new[] { "f" }, value.Fantasies);
        Assert.Equal(new[] { "tag" }, value.Tags);
    }

    [Fact]
    public void ContraceptionValue_When_RoundTripped_Should_PreserveListAndCount()
    {
        var value = RoundTrip(ContraceptionValue.Create(new[] { "c1" }, 2));
        Assert.Equal(new[] { "c1" }, value.Contraceptives);
        Assert.Equal(2, value.ContraceptiveCount);
    }

    [Fact]
    public void DurationAndOrigin_When_RoundTripped_Should_PreserveValues()
    {
        Assert.Equal(30, RoundTrip(DurationValue.Create(30)).Minutes);
        var origin = RoundTrip(OriginValue.Create(true, "g-1"));
        Assert.True(origin.Garmin);
        Assert.Equal("g-1", origin.GarminId);
    }

    [Fact]
    public void OrgasmsList_When_RoundTripped_Should_PreserveEntries()
    {
        var value = RoundTrip(new OrgasmsListValueObject(new[] { Orgasms.CreateInstance("me", 1, new[] { "kind" }, 5) }));
        var item = Assert.Single(value.Value);
        Assert.Equal("me", item.Participant);
        Assert.Equal(5, item.Rating);
    }

    [Fact]
    public void LegacyPayload_When_OldFormat_Should_DeserializeThroughFallback()
    {
        var legacyOptions = new System.Text.Json.JsonSerializerOptions();
        legacyOptions.Converters.Add(new LegacyValueObjectJsonConverter());
        var payload = System.Text.Json.JsonSerializer.Serialize<IValueObject>(PlaceValue.Create("legacy"), legacyOptions);
        var envelope = $"{{\"Place\":{payload}}}";
        var restored = Serializer.DeserializeWithLegacy(envelope, options => options.Converters.Add(new OrgasmsJsonConverter()));
        Assert.IsType<PlaceValue>(restored["Place"]);
        Assert.Equal("legacy", ((PlaceValue)restored["Place"]).Value);
    }

    [Fact]
    public void RecordRoot_When_RoundTripped_Should_PreservePartnersAndStartedAt()
    {
        var started = DateTimeOffset.Parse("2026-10-29T21:00+03:00");
        var root = RoundTrip(RecordRoot.CreateInstance(new[] { "p1" }, started));
        Assert.Equal(new[] { "p1" }, root.Partners);
        Assert.Equal(started, root.StartedAt);
    }

    [Fact]
    public void ClientIdValueObject_When_RoundTripped_Should_PreserveNullableGuid()
    {
        var id = Guid.NewGuid();
        Assert.Equal(id, RoundTrip(new ClientIdValueObject(id)).Value);
        Assert.Null(RoundTrip(new ClientIdValueObject(null)).Value);
    }

    [Fact]
    public void Dictionary_When_RoundTripped_Should_PreservePolymorphicTypes()
    {
        var source = new Dictionary<string, IValueObject>
        {
            ["Place"] = PlaceValue.Create("отель"),
            ["Duration"] = DurationValue.Create(45)
        };
        var json = Serializer.Serialize(source);
        var restored = Serializer.Deserialize(json);
        Assert.IsType<PlaceValue>(restored["Place"]);
        Assert.IsType<DurationValue>(restored["Duration"]);
        Assert.Equal("отель", ((PlaceValue)restored["Place"]).Value);
        Assert.Equal(45, ((DurationValue)restored["Duration"]).Minutes);
    }
}
