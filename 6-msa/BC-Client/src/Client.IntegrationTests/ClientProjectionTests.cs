using System;
using System.Collections.Generic;
using Client.Api.Events;
using Client.Domain.Implementation;
using Client.InternalContracts;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using Xunit;

namespace Client.IntegrationTests;

/// <summary>
/// BE-134: проекция read-модели Client. Проверяет чистую логику ClientProjection.ApplyVos —
/// что changed-VO корректно ложатся в ClientReadModel (Phone/DisplayName/Gender/PhotoUrl/Verified).
/// </summary>
public sealed class ClientProjectionTests
{
    [Fact]
    public void ApplyVos_MapsChangedFields_ToReadModel()
    {
        var vos = new Dictionary<string, IValueObject>
        {
            ["Root"] = ClientRoot.CreateInstance("+79991234567"),
            ["DisplayName"] = new StringValueObject("Alice"),
            ["Gender"] = new StringValueObject("female"),
            ["PhotoUrl"] = new NullableStringValueObject("https://example.com/a.png"),
            ["Verified"] = new BoolValueObject(true),
        };

        var m = new ClientReadModel { Id = Guid.NewGuid() };
        ClientProjection.ApplyVos(m, vos);

        Assert.Equal("+79991234567", m.Phone);
        Assert.Equal("Alice", m.DisplayName);
        Assert.Equal("female", m.Gender);
        Assert.Equal("https://example.com/a.png", m.PhotoUrl);
        Assert.True(m.Verified);
    }

    [Fact]
    public void ApplyVos_EmptyDiff_LeavesModelUntouched()
    {
        var m = new ClientReadModel { Id = Guid.NewGuid(), Phone = "keep" };
        ClientProjection.ApplyVos(m, new Dictionary<string, IValueObject>());
        Assert.Equal("keep", m.Phone);
    }
}
