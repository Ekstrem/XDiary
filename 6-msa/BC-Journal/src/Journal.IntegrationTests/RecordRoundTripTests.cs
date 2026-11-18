using System.Text.Json;
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
    [Fact]
    public void StartManualEntry_Diff_RoundTrips_ThroughConverter()
    {
        var aggregate = Aggregate.CreateInstance(new AnemicModel { Id = Guid.NewGuid() });
        var result = aggregate.StartManualEntry(Guid.NewGuid());
        Assert.True(result.IsSuccess());
        var vos = result.Event!.ChangedValueObjects!;
        Assert.NotEmpty(vos);

        var opts = new JsonSerializerOptions();
        opts.Converters.Add(new IValueObjectJsonConverter());

        var json = JsonSerializer.Serialize(vos, opts);
        var back = JsonSerializer.Deserialize<IDictionary<string, IValueObject>>(json, opts)!;

        Assert.Equal(vos.Count, back.Count);
        foreach (var key in vos.Keys)
            Assert.True(back.ContainsKey(key), $"ключ {key} потерян при round-trip");

        var root = Assert.IsAssignableFrom<IRecordRoot>(back["Root"]);
        Assert.NotNull(root.Partners);

        var clientId = Assert.IsAssignableFrom<ClientIdValueObject>(back["ClientId"]);
        Assert.Equal(((ClientIdValueObject)vos["ClientId"]).Value, clientId.Value);
    }
}
