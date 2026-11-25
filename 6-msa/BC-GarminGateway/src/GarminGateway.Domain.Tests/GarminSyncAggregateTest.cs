using XDiary.Contracts;
using DigiTFactory.Libraries.SeedWorks.Result;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;
using Xunit;

public class GarminSyncAggregateTest
{
    private const string FutureExpiry = "2999-01-01T00:00:00+00:00";
    private const string PastExpiry = "2000-01-01T00:00:00+00:00";

    private static IGarminGatewayAnemicModel ConnectedModel(string? expiry = FutureExpiry)
    {
        var clientId = Guid.NewGuid();
        return Aggregate.CreateInstance(new AnemicModel())
            .Connect(clientId, Credentials.CreateInstance("key", "token", expiry)).Model();
    }

    [Fact]
    public void Connect_WhenCredentialsValid_ShouldSucceed()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).Connect(Guid.NewGuid(), Credentials.CreateInstance("key", "token", FutureExpiry));
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void Connect_WhenApiKeyMissing_ShouldReturnValidationError()
    {
        // BE-142 (один токен): обязателен только apiKey; пустой oauthToken — норма.
        var r = Aggregate.CreateInstance(new AnemicModel()).Connect(Guid.NewGuid(), Credentials.CreateInstance("", "token", ""));
        Assert.Equal("validation_error", r.ErrorMessage());
    }

    [Fact]
    public void Connect_WhenExpiryInvalid_ShouldReturnValidationError()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).Connect(Guid.NewGuid(), Credentials.CreateInstance("key", "token", "not-a-date"));
        Assert.Equal("validation_error", r.ErrorMessage());
    }

    [Fact]
    public void Connect_WhenTokenExpired_ShouldReturnTokenExpired()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).Connect(Guid.NewGuid(), Credentials.CreateInstance("key", "token", PastExpiry));
        Assert.Equal(DomainOperationResultEnum.Exception, r.Result); // доменный результат, а не выброшенное исключение
        Assert.Equal("token_expired", r.ErrorMessage());
    }

    [Fact]
    public void Sync_WhenNotConnected_ShouldReturnNotConnected()
    {
        var r = Aggregate.CreateInstance(new AnemicModel()).Sync(SyncConfig.CreateInstance(new[] { "hr" }, 30));
        Assert.Equal("not_connected", r.ErrorMessage());
    }

    [Fact]
    public void Sync_WhenTokenExpired_ShouldReturnTokenExpired()
    {
        var model = new AnemicModel
        {
            Root = GarminSyncRoot.CreateInstance(Guid.NewGuid(), "garmin"),
            Credentials = Credentials.CreateInstance("key", "token", PastExpiry),
        };
        var r = Aggregate.CreateInstance(model).Sync(SyncConfig.CreateInstance(new[] { "hr" }, 30));
        Assert.Equal("token_expired", r.ErrorMessage());
    }

    [Fact]
    public void Sync_WhenWindowDaysInvalid_ShouldReturnValidationError()
    {
        var r = Aggregate.CreateInstance(ConnectedModel()).Sync(SyncConfig.CreateInstance(new[] { "hr" }, 0));
        Assert.Equal("validation_error", r.ErrorMessage());
    }

    [Fact]
    public void Sync_WhenConnected_ShouldSucceed()
    {
        var r = Aggregate.CreateInstance(ConnectedModel()).Sync(SyncConfig.CreateInstance(new[] { "hr", "bb" }, 30));
        Assert.Equal(DomainOperationResultEnum.Success, r.Result);
    }

    [Fact]
    public void FetchActivities_WhenTokenExpired_ShouldReturnTokenExpired()
    {
        var model = new AnemicModel
        {
            Root = GarminSyncRoot.CreateInstance(Guid.NewGuid(), "garmin"),
            Credentials = Credentials.CreateInstance("key", "token", PastExpiry),
        };
        var r = Aggregate.CreateInstance(model).FetchActivities();
        Assert.Equal("token_expired", r.ErrorMessage());
    }
}
