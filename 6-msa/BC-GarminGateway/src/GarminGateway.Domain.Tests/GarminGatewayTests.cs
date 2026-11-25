using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;
using Xunit;

namespace XDiary.Tests.Gateways;

/// <summary>TE-004: garmin-gateway — креды + раздельные потоки «соматика» и «занятия».</summary>
public class GarminGatewayTests
{
    [Fact]
    public void Connect_WhenServerAcquiredToken_Succeeds()
    {
        // BE-142 (один токен): в API только apiKey; oauthToken сервер получает сам.
        var r = Aggregate.CreateInstance(new AnemicModel())
            .Connect(Guid.NewGuid(), Credentials.CreateInstance("GCDC_x", "server-acquired", "2999-01-01T00:00:00+00:00"));

        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
        Assert.Equal("GCDC_x", r.Model().Credentials.ApiKey);
    }

    [Fact]
    public void Connect_WhenOauthTokenNull_Succeeds()
    {
        // BE-142 (@AR 21:32): домен не требует OauthToken — серверный маркер опционален.
        var r = Aggregate.CreateInstance(new AnemicModel())
            .Connect(Guid.NewGuid(), Credentials.CreateInstance("GCDC_x", null, "2999-01-01T00:00:00+00:00"));

        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
    }

    [Theory]
    [InlineData("", "token")]
    [InlineData("   ", "token")]
    public void Connect_InvalidCredentials_Fails(string key, string token)
    {
        // BE-142 (один токен): невалиден только пустой/пробельный apiKey
        // (пустой oauthToken — норма, сервер получает его сам).
        var r = Aggregate.CreateInstance(new AnemicModel())
            .Connect(Guid.NewGuid(), Credentials.CreateInstance(key, token, ""));
        Assert.NotEqual(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void Sync_Somatic_And_FetchActivities_AreSeparateFlows()
    {
        var connected = Aggregate.CreateInstance(new AnemicModel())
            .Connect(Guid.NewGuid(), Credentials.CreateInstance("key", "token", "")).Model();

        // Поток 1 — соматика: Sync(config), сохраняет метрики и lastSync.
        var synced = Aggregate.CreateInstance(connected)
            .Sync(SyncConfig.CreateInstance(new[] { "hr", "body_battery" }, 7));
        Assert.Equal(DomainOperationResultEnum.Success, synced.Result);
        Assert.Equal(2, synced.Model().SyncConfig.Metrics.Count);
        Assert.False(string.IsNullOrEmpty(synced.Model().LastSync));

        // Поток 2 — занятия: FetchActivities(), отдельная сущность, не трогает соматику.
        var acts = Aggregate.CreateInstance(synced.Model()).FetchActivities();
        Assert.Equal(DomainOperationResultEnum.Success, acts.Result);
        Assert.Equal(2, acts.Model().SyncConfig.Metrics.Count);
        Assert.False(string.IsNullOrEmpty(acts.Model().LastSync));
    }
}
