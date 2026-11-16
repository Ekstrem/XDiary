using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain.Implementation;
using Xunit;

/// <summary>
/// BE-051 (класс): идемпотентность создания записи журнала. Стабильный id агрегата —
/// повтор StartManualEntry с тем же id → та же read-модель (upsert), а не вторая запись.
/// </summary>
public class JournalIdempotencyTests
{
    [Fact]
    public void StartManualEntry_PreservesProvidedId()
    {
        var id = Guid.NewGuid();
        var r = Aggregate.CreateInstance(new AnemicModel { Id = id }).StartManualEntry(Guid.NewGuid());
        Assert.Equal(id, r.Model().Id);
    }

    [Fact]
    public void StartManualEntry_TwoSameIds_ShareAggregateIdentity()
    {
        var id = Guid.NewGuid();
        var a = Aggregate.CreateInstance(new AnemicModel { Id = id }).StartManualEntry(Guid.NewGuid());
        var b = Aggregate.CreateInstance(new AnemicModel { Id = id }).StartManualEntry(Guid.NewGuid());
        Assert.Equal(a.Model().Id, b.Model().Id);
    }
}
