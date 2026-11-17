using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Partners.Domain.Implementation;
using Xunit;

/// <summary>
/// BE-051 (класс): идемпотентность создания партнёра. Стабильный id агрегата — повтор
/// Register с тем же id → та же read-модель (upsert), а не вторая запись.
/// </summary>
public class PartnersIdempotencyTests
{
    [Fact]
    public void Register_PreservesProvidedId()
    {
        var id = Guid.NewGuid();
        var r = Aggregate.CreateInstance(new AnemicModel { Id = id }).Register("partner", Guid.NewGuid());
        Assert.Equal(id, r.Model().Id);
    }

    [Fact]
    public void Register_TwoSameIds_ShareAggregateIdentity()
    {
        var id = Guid.NewGuid();
        var a = Aggregate.CreateInstance(new AnemicModel { Id = id }).Register("partner", Guid.NewGuid());
        var b = Aggregate.CreateInstance(new AnemicModel { Id = id }).Register("partner", Guid.NewGuid());
        Assert.Equal(a.Model().Id, b.Model().Id);
    }
}
