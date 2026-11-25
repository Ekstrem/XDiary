using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Serialization;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Journal.Domain.Abstraction;
using Journal.Domain.Implementation;
using XDiary.Contracts;
using Xunit;

/// <summary>
/// BE-129: round-trip ChangedValueObjects → JSON → ChangedValueObjects через IValueObjectJsonConverter.
/// Ловит асимметрию сериализация↔десериализация (internal record не резолвится по $type и т.п.).
/// </summary>
public class RecordRoundTripTests
{
    private static CommandToAggregate Cmd(string name)
        => CommandToAggregate.Commit(Guid.NewGuid(), name, "test", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    [Fact]
    public void StartManualEntry_Diff_RoundTrips_ThroughConverter()
    {
        var aggregate = Aggregate.CreateInstance(new AnemicModel { Id = Guid.NewGuid() });
        var result = aggregate.StartManualEntry(Guid.NewGuid(), Cmd("StartManualEntry"));
        Assert.True(result.IsSuccess());
        var vos = result.Event!.ChangedValueObjects!;
        Assert.NotEmpty(vos);

        var serializer = ValueObjectSerializerProvider.Create();

        var json = serializer.Serialize(vos as IReadOnlyDictionary<string, IValueObject>);
        var back = serializer.Deserialize(json)!;

        Assert.Equal(vos.Count, back.Count);
        foreach (var key in vos.Keys)
            Assert.True(back.ContainsKey(key), $"ключ {key} потерян при round-trip");

        var root = Assert.IsAssignableFrom<IRecordRoot>(back["Root"]);
        Assert.NotNull(root.Partners);

        var clientId = Assert.IsAssignableFrom<ClientIdValueObject>(back["ClientId"]);
        Assert.Equal(((ClientIdValueObject)vos["ClientId"]).Value, clientId.Value);
    }
}
