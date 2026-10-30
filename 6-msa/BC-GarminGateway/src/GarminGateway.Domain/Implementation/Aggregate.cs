namespace GarminGateway.Domain.Implementation;
using DigiTFactory.Libraries.SeedWorks.Invariants;
using DigiTFactory.Libraries.SeedWorks.Result;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Specifications;
using XDiary.Contracts;

public sealed class Aggregate
{
    public IGarminGatewayAnemicModel Model { get; }
    private Aggregate(IGarminGatewayAnemicModel model) => Model = model;
    public static Aggregate CreateInstance(IGarminGatewayAnemicModel model) => new(model);

    private AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Success(AnemicModel m)
    { var d = BusinessOperationData<IGarminGateway, IGarminGatewayAnemicModel>.Commit<IGarminGateway, IGarminGatewayAnemicModel>(Model, m); return new AggregateResultSuccess<IGarminGateway, IGarminGatewayAnemicModel>(d); }
    private AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Fail(string e)
    { var d = BusinessOperationData<IGarminGateway, IGarminGatewayAnemicModel>.Commit<IGarminGateway, IGarminGatewayAnemicModel>(Model, Model); return new AggregateResultException<IGarminGateway, IGarminGatewayAnemicModel>(d, new FailedSpecification<IGarminGateway, IGarminGatewayAnemicModel>(e)); }
    private AnemicModel Clone() => new AnemicModel { Id = Model.Id, Version = Model.Version, CorrelationToken = Model.CorrelationToken, Root = Model.Root, Credentials = Model.Credentials, SyncConfig = Model.SyncConfig, LastSync = Model.LastSync };

    public AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Connect(ICredentials credentials)
    {
        var v = new CredentialsValidator();
        if (!v.IsSatisfiedBy(credentials)) return Fail(v.Reason);
        var m = Clone(); m.Root = GarminSyncRoot.CreateInstance("garmin"); m.Credentials = credentials; return Success(m);
    }
    public AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> Sync(ISyncConfig config)
    {
        var m = Clone(); m.SyncConfig = config; m.LastSync = DateTimeOffset.UtcNow.ToString("o"); return Success(m);
    }
    public AggregateResult<IGarminGateway, IGarminGatewayAnemicModel> FetchActivities()
    {
        var m = Clone(); m.LastSync = DateTimeOffset.UtcNow.ToString("o"); return Success(m);
    }
}