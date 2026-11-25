namespace GarminGateway.Domain.Implementation;
using System.Globalization;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Specifications;
using XDiary.Contracts;

internal sealed class Aggregate : IGarminGatewayAggregate
{
    public IGarminGatewayAnemicModel Model { get; }
    private Aggregate(IGarminGatewayAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IGarminGatewayAnemicModel model) => new(model);

    // IAggregate<TBC> / IAnemicModel<TBC>:
    public Guid Id => Model.Id;
    public long Version => Model.Version;
    public Guid CorrelationToken => Model.CorrelationToken;
    public string CommandName => Model.CommandName;
    public string SubjectName => Model.SubjectName;
    public IDictionary<string, IValueObject> Invariants => Model.Invariants;
    public IDictionary<string, IValueObject> GetValueObjects() => Model.GetValueObjects();

    // IGarminGatewayAnemicModel:
    public IGarminSyncRoot Root => Model.Root;
    public ICredentials Credentials => Model.Credentials;
    public ISyncConfig SyncConfig => Model.SyncConfig;
    public string LastSync => Model.LastSync;
    private AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Success(AnemicModel m, CommandToAggregate? command = null, [System.Runtime.CompilerServices.CallerMemberName] string commandName = "")
    {
        command ??= CommandToAggregate.Commit(m.CorrelationToken, commandName, m.SubjectName, Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Model.Version + 1));
        m = m with { Version = command.Version, CommandName = command.CommandName, SubjectName = command.SubjectName }; // BE-150: Command.Version = новая версия агрегата (timestamp), acted-on Version остаётся в DomainEvent.Version
        var d = BusinessOperationData<IGarminGateway, IGarminGatewayAnemicModel>.Commit<IGarminGateway, IGarminGatewayAnemicModel>(Model, m); return new AggregateResultSuccess<IGarminGateway, IGarminGatewayAnemicModel>(d); }
    private AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Fail(string e)
    { var d = BusinessOperationData<IGarminGateway, IGarminGatewayAnemicModel>.Commit<IGarminGateway, IGarminGatewayAnemicModel>(Model, Model); return new AggregateResultException<IGarminGateway, IGarminGatewayAnemicModel>(d, new FailedSpecification<IGarminGateway, IGarminGatewayAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Credentials = Model.Credentials, SyncConfig = Model.SyncConfig, LastSync = Model.LastSync };

    public AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Connect(Guid clientId, ICredentials credentials, CommandToAggregate? command = null)
    {
        if (credentials is null) return Fail("validation_error");
        var v = new CredentialsSpecification();
        if (!v.IsSatisfiedBy(credentials)) return Fail("validation_error");
        if (!IsValidExpiry(credentials.TokenExpiry)) return Fail("validation_error");
        if (IsExpired(credentials.TokenExpiry)) return Fail("token_expired");
        var s = new SingleSourcePerClientSpecification();
        if (!s.IsSatisfiedBy(clientId, Model)) return Fail("validation_error");
        return Success(Clone() with { Root = GarminSyncRoot.CreateInstance(clientId, "garmin"), Credentials = credentials }, command);
    }
    public AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Sync(ISyncConfig config, CommandToAggregate? command = null)
    {
        if (!IsConnected()) return Fail("not_connected");
        if (config is null || config.WindowDays <= 0) return Fail("validation_error");
        if (IsExpired(Model.Credentials.TokenExpiry)) return Fail("token_expired");
        return Success(Clone() with { SyncConfig = config, LastSync = DateTimeOffset.UtcNow.ToString("o") }, command);
    }
    public AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> FetchActivities(CommandToAggregate? command = null)
    {
        if (!IsConnected()) return Fail("not_connected");
        if (IsExpired(Model.Credentials.TokenExpiry)) return Fail("token_expired");
        return Success(Clone() with { LastSync = DateTimeOffset.UtcNow.ToString("o") }, command);
    }

    private bool IsConnected()
        => Model.Root is not null
           && !string.IsNullOrWhiteSpace(Model.Credentials.ApiKey)
           && !string.IsNullOrWhiteSpace(Model.Credentials.OauthToken);

    private static bool IsValidExpiry(string? value)
        => string.IsNullOrWhiteSpace(value)
           || DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _);

    private static bool IsExpired(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiry)
           && expiry <= DateTimeOffset.UtcNow;
}