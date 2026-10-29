using DigiTFactory.Libraries.AbstractAggregate.DynamicTypes;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Privacy.Domain;
using Xunit;

namespace Privacy.Domain.Tests;

/// <summary>
/// Доменные тесты агрегата PinCredential (metadata-driven, in-memory).
/// </summary>
public class PinCredentialAggregateTest
{
    [Fact]
    public void SetPin_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = PrivacyMetadataHelper.CreateNewAggregate(id);
        var model = PinCredentialBuilder.NewCredentialModel(id);

        var result = aggregate.SetPin(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void SetPin_OverExisting_Succeeds()
    {
        var id = Guid.NewGuid();
        var aggregate = PrivacyMetadataHelper.CreateAggregate(PinCredentialBuilder.ExistingCredentialModel(id));
        var model = PinCredentialBuilder.NewCredentialModel(id);

        // Upsert: повторная установка PIN не должна падать (нет IsNewEntity)
        var result = aggregate.SetPin(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void SetPin_MissingUserId_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = PrivacyMetadataHelper.CreateNewAggregate(id);

        var vos = PinCredentialBuilder.BuildValueObjects();
        vos["Root"] = new DynamicValueObject("PinCredentialRoot", new Dictionary<string, object?>
        {
            ["UserId"] = null
        });
        var model = DynamicAnemicModel<IPrivacy>.Create(id, 0, Guid.NewGuid(), "SetPin", "SetPin", vos);

        var result = aggregate.SetPin(model, model.CommandName.CreateCommandMetadata());

        // Инвариант PropertyNotNull:Root.UserId должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void RemovePin_HappyPath()
    {
        var id = Guid.NewGuid();
        var aggregate = PrivacyMetadataHelper.CreateAggregate(PinCredentialBuilder.ExistingCredentialModel(id));
        var model = PinCredentialBuilder.NewCredentialModel(id);

        var result = aggregate.RemovePin(model, model.CommandName.CreateCommandMetadata());

        Assert.Equal(DomainOperationResultEnum.Success, result.Result);
    }

    [Fact]
    public void RemovePin_OnNew_Fails()
    {
        var id = Guid.NewGuid();
        var aggregate = PrivacyMetadataHelper.CreateNewAggregate(id);
        var model = PinCredentialBuilder.NewCredentialModel(id);

        var result = aggregate.RemovePin(model, model.CommandName.CreateCommandMetadata());

        // Инвариант IsNotNewEntity должен провалиться
        Assert.NotEqual(DomainOperationResultEnum.Success, result.Result);
    }
}
