using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Partners.Domain;
using Xunit;

namespace Partners.Domain.Tests;

/// <summary>
/// Доменные тесты агрегата Partner (metadata-driven, in-memory).
/// </summary>
public class PartnerAggregateTest
{
    [Fact]
    public void CreatePartner_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = PartnersMetadataHelper.CreateNewAggregate(id);

        var result = aggregate.CreatePartner(PartnerBuilder.NewPartnerModel(id));

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void CreatePartner_AlreadyExists_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = PartnersMetadataHelper.CreateAggregate(PartnerBuilder.ExistingPartnerModel(id));

        var result = aggregate.CreatePartner(PartnerBuilder.NewPartnerModel(id));

        // Инвариант IsNewEntity должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void CreatePartner_MissingName_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = PartnersMetadataHelper.CreateNewAggregate(id);

        var vos = PartnerBuilder.BuildValueObjects();
        vos["Root"] = new DynamicValueObject("PartnerRoot", new Dictionary<string, object?>
        {
            ["Name"] = null
        });
        var model = DynamicAnemicModel<IPartners>.Create(id, 0, Guid.NewGuid(), "CreatePartner", "CreatePartner", vos);

        var result = aggregate.CreatePartner(model);

        // Инвариант PropertyNotNull:Root.Name должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpdatePartner_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = PartnersMetadataHelper.CreateAggregate(PartnerBuilder.ExistingPartnerModel(id));
        var model = PartnerBuilder.UpdatePartnerModel(id);

        var result = aggregate.UpdatePartner(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void UpdatePartner_NotExists_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = PartnersMetadataHelper.CreateNewAggregate(id);
        var model = PartnerBuilder.UpdatePartnerModel(id);

        var result = aggregate.UpdatePartner(model, model.CommandName.CreateCommandMetadata());

        // Инвариант IsNotNewEntity должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void DeletePartner_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = PartnersMetadataHelper.CreateAggregate(PartnerBuilder.ExistingPartnerModel(id));
        var model = PartnerBuilder.DeletePartnerModel(id);

        var result = aggregate.DeletePartner(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void DeletePartner_NotExists_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = PartnersMetadataHelper.CreateNewAggregate(id);
        var model = PartnerBuilder.DeletePartnerModel(id);

        var result = aggregate.DeletePartner(model, model.CommandName.CreateCommandMetadata());

        // Инвариант IsNotNewEntity должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
}
