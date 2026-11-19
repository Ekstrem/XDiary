using Catalog.Api.Events;
using Catalog.Domain.Implementation;
using Catalog.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using XDiary.Contracts;
using Xunit;

namespace Catalog.IntegrationTests;

/// <summary>
/// BE-140 L1: проекция события → materialized view. Проверяет, что проекция маппит ВСЕ поля,
/// которые есть в `GetValueObjects()` (класс BE-134/137/138: «поле есть в модели, но не в проекции»).
/// </summary>
public class CatalogProjectionTests
{
    private static Catalog.Domain.Abstraction.ICatalogAnemicModel FullModel()
    {
        var clientId = Guid.NewGuid();
        var start = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", "Ball", clientId);
        var details = Aggregate.CreateInstance(start.Model()).UpdateDetails("photo", "notes", "Brand", "Аня");
        return Aggregate.CreateInstance(details.Model()).Archive().Model();
    }

    [Fact]
    public void ApplyVos_Maps_EveryFieldFromGetValueObjects()
    {
        var m = FullModel();
        var read = new CatalogReadModel { Id = m.Id };

        CatalogProjection.ApplyVos(read, m.GetValueObjects());

        Assert.Equal("toy", read.Kind);
        Assert.Equal("Ball", read.Name);
        Assert.Equal(m.ClientId, read.ClientId);
        Assert.Equal("photo", read.Photo);
        Assert.Equal("notes", read.Notes);
        Assert.Equal("Brand", read.Brand);
        Assert.True(read.Archived);
        Assert.Equal("Аня", read.FantasyPartner);
    }

    [Fact]
    public void ApplyVos_CoversAllKeysOfGetValueObjects()
    {
        var m = FullModel();
        var read = new CatalogReadModel { Id = m.Id };
        CatalogProjection.ApplyVos(read, m.GetValueObjects());

        // Все ключи модели должны быть отражены в read-модели (нет «забытых» полей).
        var keys = m.GetValueObjects().Keys.ToHashSet();
        Assert.Subset(keys, new HashSet<string> { "Root", "ClientId", "Photo", "Notes", "Brand", "Archived", "FantasyPartner" });
    }

    [Fact]
    public void ApplyVos_PerField_DoesNotWipeUnchanged()
    {
        var read = new CatalogReadModel { Id = Guid.NewGuid(), Kind = "toy", Name = "Ball", Brand = "LELO", Photo = "p" };

        CatalogProjection.ApplyVos(read, new Dictionary<string, IValueObject>
        {
            ["Notes"] = new StringValueObject("заметка"),
        });

        Assert.Equal("заметка", read.Notes);
        Assert.Equal("toy", read.Kind);
        Assert.Equal("Ball", read.Name);
        Assert.Equal("LELO", read.Brand);
        Assert.Equal("p", read.Photo);
    }

    [Fact]
    public void ApplyVos_EmptyDiff_NoOp()
    {
        var read = new CatalogReadModel { Id = Guid.NewGuid(), Kind = "toy", Name = "Ball" };
        CatalogProjection.ApplyVos(read, new Dictionary<string, IValueObject>());
        Assert.Equal("toy", read.Kind);
        Assert.Equal("Ball", read.Name);
    }
}
