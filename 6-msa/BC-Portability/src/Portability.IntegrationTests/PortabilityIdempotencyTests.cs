using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using Portability.Domain.Implementation;
using Xunit;

/// <summary>
/// BE-051 (класс): идемпотентность импорта. Стабильный id документа — повтор импорта
/// с тем же id → та же запись (upsert), а не дубль.
/// </summary>
public class PortabilityIdempotencyTests
{
    [Fact]
    public void ImportRecords_PreservesProvidedId()
    {
        var id = Guid.NewGuid();
        var r = Aggregate.CreateInstance(new AnemicModel { Id = id }).ImportRecords(new[] { "row-1" }, Guid.NewGuid());
        Assert.Equal(id, r.Model().Id);
    }
}
