using System.Text.Json;
using GarminGateway.Api.Contracts;
using GarminGateway.Domain.Implementation;
using GarminGateway.InternalContracts;
using XDiary.Contracts;
using Xunit;

namespace GarminGateway.IntegrationTests;

/// <summary>BE-142: публичный ответ не раскрывает credentials и корректно вычисляет requiresReauth.</summary>
public class GarminConnectionStateTests
{
    [Fact]
    public void FromAnemicModel_WhenConnected_ShouldNotExposeSecrets()
    {
        var result = Aggregate.CreateInstance(new AnemicModel())
            .Connect(Guid.NewGuid(), Credentials.CreateInstance("secret-key", "secret-token", "2999-01-01T00:00:00+00:00"));

        var state = GarminConnectionState.FromAnemicModel(result.Model());
        var json = JsonSerializer.Serialize(state);

        Assert.True(state.Connected);
        Assert.False(state.RequiresReauth);
        Assert.DoesNotContain("secret-key", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("oauthToken", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromReadModel_WhenTokenExpired_ShouldRequireReauthWithoutSecrets()
    {
        var model = new GarminSyncReadModel
        {
            Id = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            SourceId = "garmin",
            ApiKey = "secret-key",
            OauthToken = "secret-token",
            TokenExpiry = "2000-01-01T00:00:00+00:00",
            Metrics = new[] { "hr" },
            WindowDays = 30,
        };

        var state = GarminConnectionState.FromReadModel(model);
        var json = JsonSerializer.Serialize(state);

        Assert.True(state.Connected);
        Assert.True(state.RequiresReauth);
        Assert.Equal("garmin", state.SourceId);
        Assert.DoesNotContain("secret-key", json, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", json, StringComparison.Ordinal);
    }

    [Fact]
    public void FromReadModel_WhenExpiryUnknown_ShouldNotRequireReauth()
    {
        var model = new GarminSyncReadModel { SourceId = "garmin", TokenExpiry = string.Empty };

        var state = GarminConnectionState.FromReadModel(model);

        Assert.False(state.RequiresReauth);
        Assert.Null(state.TokenExpiry);
    }
}
