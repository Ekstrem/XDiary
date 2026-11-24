namespace GarminGateway.Application.Commands;
using GarminGateway.Application;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using GarminGateway.Domain;
using GarminGateway.Domain.Abstraction;
using GarminGateway.Domain.Implementation;
using GarminGateway.InternalContracts;
using XDiary.Contracts;

public sealed class ConnectHandler : IRequestHandler<ConnectCommand, AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IGarminGateway, IGarminGatewayAggregate> _provider;
    private readonly IObserver<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IGarminGateway, GarminSyncReadModel> _read;
    public ConnectHandler(IAggregateProvider<IGarminGateway, IGarminGatewayAggregate> provider, IObserver<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> observer, IActorProvider actor, IReadRepository<IGarminGateway, GarminSyncReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> Handle(ConnectCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.ClientId == _actor.ClientId && x.SourceId == "garmin");
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IGarminGateway, IGarminGatewayAggregate>)_provider).GetAggregateWithCompletionAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>)aggregate).Subscribe(_observer);
        var creds = Credentials.CreateInstance(r.Credentials.ApiKey, r.Credentials.OauthToken, r.Credentials.TokenExpiry);
        var result = aggregate.Connect(_actor.ClientId, creds, AggregateCommandFactory.Create("Connect"));
        await completion;
        return result;
    }
}
public sealed class SyncHandler : IRequestHandler<SyncCommand, AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IGarminGateway, IGarminGatewayAggregate> _provider;
    private readonly IObserver<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IGarminGateway, GarminSyncReadModel> _read;
    public SyncHandler(IAggregateProvider<IGarminGateway, IGarminGatewayAggregate> provider, IObserver<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> observer, IActorProvider actor, IReadRepository<IGarminGateway, GarminSyncReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> Handle(SyncCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.ClientId == _actor.ClientId && x.SourceId == "garmin");
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IGarminGateway, IGarminGatewayAggregate>)_provider).GetAggregateWithCompletionAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>)aggregate).Subscribe(_observer);
        var cfg = SyncConfig.CreateInstance(r.Config.Metrics, r.Config.WindowDays);
        var result = aggregate.Sync(cfg, AggregateCommandFactory.Create("Sync"));
        await completion;
        return result;
    }
}
public sealed class FetchActivitiesHandler : IRequestHandler<FetchActivitiesCommand, AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IGarminGateway, IGarminGatewayAggregate> _provider;
    private readonly IObserver<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IGarminGateway, GarminSyncReadModel> _read;
    public FetchActivitiesHandler(IAggregateProvider<IGarminGateway, IGarminGatewayAggregate> provider, IObserver<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> observer, IActorProvider actor, IReadRepository<IGarminGateway, GarminSyncReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>> Handle(FetchActivitiesCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.ClientId == _actor.ClientId && x.SourceId == "garmin");
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<IGarminGateway, IGarminGatewayAggregate>)_provider).GetAggregateWithCompletionAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IGarminGateway, IGarminGatewayAnemicModel>>)aggregate).Subscribe(_observer);
        var result = aggregate.FetchActivities(AggregateCommandFactory.Create("FetchActivities"));
        await completion;
        return result;
    }
}
