using XDiary.Contracts;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.Domain.Implementation;
using Catalog.InternalContracts;
using Catalog.Storage;
using DigiTFactory.Libraries.SeedWorks.Result;
using Xunit;

namespace XDiary.Tests.PartnersCatalog;

/// <summary>TE-002: catalog — CRUD по kind (costume/toy/fantasy/contraception) + архив игрушек.</summary>
public class CatalogTests
{
    private static ICatalogAnemicModel AddItem(string kind, string name)
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).AddItem(kind, name);
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
        return r.Model();
    }

    private static CatalogReadModel ToRead(ICatalogAnemicModel m)
    {
        var e = new CatalogItemEntity();
        CatalogMapper.MapToEntity(m, e);
        return CatalogMapper.MapToReadModel(e);
    }

    [Theory]
    [InlineData("costume", "костюм медсестры")]
    [InlineData("toy", "вибратор")]
    [InlineData("fantasy", "ролевая игра")]
    [InlineData("contraception", "презерватив")]
    public void AddItem_AllKinds_Succeed(string kind, string name)
    {
        var read = ToRead(AddItem(kind, name));
        Assert.Equal(kind, read.Kind);
        Assert.Equal(name, read.Name);
    }

    [Fact]
    public void AddItem_InvalidKind_Fails()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).AddItem("unknown", "x");
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void AddItem_EmptyName_Fails()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).AddItem("toy", " ");
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void UpdateDetails_BrandOnToy_Succeeds()
    {
        var model = AddItem("toy", "вибратор");
        var r = Aggregate.CreateInstance(model).UpdateDetails("", "заметка", "LELO", "");
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
        Assert.Equal("LELO", ToRead(r.Model()).Brand);
    }

    [Fact]
    public void UpdateDetails_BrandOnCostume_Fails()
    {
        var model = AddItem("costume", "костюм");
        var r = Aggregate.CreateInstance(model).UpdateDetails("", "", "LELO", "");
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void Fantasy_WithPartner_Query_ReturnsPartner()
    {
        var model = AddItem("fantasy", "ролевая");
        model = Aggregate.CreateInstance(model).UpdateDetails("", "описание", "", "Аня").Model();
        Assert.Equal("Аня", ToRead(model).FantasyPartner);
    }

    [Fact]
    public void Archive_Restore_OnlyForToy()
    {
        var model = AddItem("toy", "вибратор");

        var archived = Aggregate.CreateInstance(model).Archive();
        Assert.Equal(DomainOperationResultEnum.Success, archived.Result);
        Assert.True(ToRead(archived.Model()).Archived);

        var restored = Aggregate.CreateInstance(archived.Model()).Restore();
        Assert.Equal(DomainOperationResultEnum.Success, restored.Result);
        Assert.False(ToRead(restored.Model()).Archived);
    }

    [Fact]
    public void Archive_OnFantasy_Fails()
    {
        var r = Aggregate.CreateInstance(AddItem("fantasy", "ролевая")).Archive();
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void Restore_OnContraception_Fails()
    {
        var r = Aggregate.CreateInstance(AddItem("contraception", "презерватив")).Restore();
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void Remove_Succeeds()
    {
        var r = Aggregate.CreateInstance(AddItem("costume", "костюм")).Remove();
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
    }
}
