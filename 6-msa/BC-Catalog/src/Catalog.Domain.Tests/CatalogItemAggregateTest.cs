using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Catalog.Domain;
using Xunit;

namespace Catalog.Domain.Tests;

/// <summary>
/// Доменные тесты агрегата CatalogItem (metadata-driven, in-memory).
/// </summary>
public class CatalogItemAggregateTest
{
    [Fact]
    public void CreateItem_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = CatalogMetadataHelper.CreateNewAggregate(id);

        var result = aggregate.CreateItem(CatalogItemBuilder.NewCatalogItemModel(id));

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void CreateItem_AlreadyExists_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = CatalogMetadataHelper.CreateAggregate(CatalogItemBuilder.ExistingCatalogItemModel(id));

        var result = aggregate.CreateItem(CatalogItemBuilder.NewCatalogItemModel(id));

        // Инвариант IsNewEntity должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void CreateItem_MissingName_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = CatalogMetadataHelper.CreateNewAggregate(id);

        var vos = CatalogItemBuilder.BuildValueObjects();
        vos["Root"] = new DynamicValueObject("ItemRoot", new Dictionary<string, object?>
        {
            ["Kind"] = "toy",
            ["Name"] = null
        });
        var model = DynamicAnemicModel<ICatalog>.Create(id, 0, Guid.NewGuid(), "CreateItem", "CreateItem", vos);

        var result = aggregate.CreateItem(model);

        // Инвариант PropertyNotNull:Root.Name должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpdateItem_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = CatalogMetadataHelper.CreateAggregate(CatalogItemBuilder.ExistingCatalogItemModel(id));
        var model = CatalogItemBuilder.UpdateCatalogItemModel(id);

        var result = aggregate.UpdateItem(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpdateItem_NotExists_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = CatalogMetadataHelper.CreateNewAggregate(id);
        var model = CatalogItemBuilder.UpdateCatalogItemModel(id);

        var result = aggregate.UpdateItem(model, model.CommandName.CreateCommandMetadata());

        // Инвариант IsNotNewEntity должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void DeleteItem_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = CatalogMetadataHelper.CreateAggregate(CatalogItemBuilder.ExistingCatalogItemModel(id));
        var model = CatalogItemBuilder.DeleteCatalogItemModel(id);

        var result = aggregate.DeleteItem(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void DeleteItem_NotExists_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = CatalogMetadataHelper.CreateNewAggregate(id);
        var model = CatalogItemBuilder.DeleteCatalogItemModel(id);

        var result = aggregate.DeleteItem(model, model.CommandName.CreateCommandMetadata());

        // Инвариант IsNotNewEntity должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
}
