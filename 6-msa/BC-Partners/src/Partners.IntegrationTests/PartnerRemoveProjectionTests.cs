using System.Text.Json;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Api.Events;
using Partners.Domain.Implementation;
using XDiary.Contracts;
using Xunit;

/// <summary>
/// BE-131 диагностика: @TL 00:40 — после фикса stale-skip `Version` проставляется,
/// но `DeletedAt` не попадает в read-модель. Этот тест ловит, теряется ли `DeletedAt`
/// в (а) самом ChangedValueObjects события Remove и (б) round-trip через JSON-конвертер.
/// </summary>
public class PartnerRemoveProjectionTests
{
    [Fact]
    public void RemoveEvent_HasDeletedAt_InDiff()
    {
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", Guid.NewGuid());
        var removed = Aggregate.CreateInstance(reg.Model()).Remove();
        Assert.True(removed.IsSuccess());

        var vos = removed.Event!.ChangedValueObjects!;
        Assert.True(vos.TryGetValue("DeletedAt", out var d), "ChangedValueObjects не содержит ключ DeletedAt");
        var dv = Assert.IsType<DeletedAtValueObject>(d);
        Assert.NotNull(dv.Value);

        // проекция на in-memory событии
        var read = PartnerProjection.Project(removed.Event);
        Assert.NotNull(read);
        Assert.NotNull(read!.DeletedAt);
    }

    [Fact]
    public void RemoveDiff_RoundTrips_DeletedAt_ThroughConverter()
    {
        var reg = Aggregate.CreateInstance(new AnemicModel()).Register("Алиса", Guid.NewGuid());
        var removed = Aggregate.CreateInstance(reg.Model()).Remove();
        var vos = removed.Event!.ChangedValueObjects!;

        var opts = new JsonSerializerOptions();
        opts.Converters.Add(new IValueObjectJsonConverter());
        var json = JsonSerializer.Serialize(vos, opts);
        var back = JsonSerializer.Deserialize<IDictionary<string, IValueObject>>(json, opts)!;

        Assert.True(back.TryGetValue("DeletedAt", out var d), "DeletedAt потерян при round-trip");
        var dv = Assert.IsType<DeletedAtValueObject>(d);
        Assert.NotNull(dv.Value);
    }
}
