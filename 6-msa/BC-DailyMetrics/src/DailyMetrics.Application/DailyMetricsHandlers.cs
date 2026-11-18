namespace DailyMetrics.Application.Commands;
using DailyMetrics.Application;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.Domain.Implementation;
using XDiary.Contracts;
using DailyMetrics.InternalContracts;

public sealed class RecordStressHandler : IRequestHandler<RecordStressCommand, AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate> _provider;
    private readonly IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IDailyMetrics, DailyMetricsReadModel> _read;
    private readonly IDailyMetricsEventPublisher _publisher;
    public RecordStressHandler(IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate> provider, IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> observer, IActorProvider actor, IReadRepository<IDailyMetrics, DailyMetricsReadModel> read, IDailyMetricsEventPublisher publisher) { _provider = provider; _observer = observer; _actor = actor; _read = read; _publisher = publisher; }
    public async Task<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> Handle(RecordStressCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.Date == r.Date && x.ClientId == _actor.ClientId);
        var aggregate = await _provider.GetAggregateAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>)aggregate).Subscribe(_observer);
        var res = aggregate.RecordStress(r.Date, r.Stress, _actor.ClientId);
        if (res.IsSuccess()) { await _publisher.PublishStressAsync(r.Date, r.Stress, ct); }
        return res;
    }
}
public sealed class RecordRestingHrHandler : IRequestHandler<RecordRestingHrCommand, AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate> _provider;
    private readonly IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IDailyMetrics, DailyMetricsReadModel> _read;
    public RecordRestingHrHandler(IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate> provider, IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> observer, IActorProvider actor, IReadRepository<IDailyMetrics, DailyMetricsReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> Handle(RecordRestingHrCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.Date == r.Date && x.ClientId == _actor.ClientId);
        var aggregate = await _provider.GetAggregateAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.RecordRestingHr(r.Date, r.RestingHr, _actor.ClientId);
    }
}
public sealed class RecordHrvHandler : IRequestHandler<RecordHrvCommand, AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate> _provider;
    private readonly IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IDailyMetrics, DailyMetricsReadModel> _read;
    public RecordHrvHandler(IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate> provider, IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> observer, IActorProvider actor, IReadRepository<IDailyMetrics, DailyMetricsReadModel> read) { _provider = provider; _observer = observer; _actor = actor; _read = read; }
    public async Task<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> Handle(RecordHrvCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.Date == r.Date && x.ClientId == _actor.ClientId);
        var aggregate = await _provider.GetAggregateAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>)aggregate).Subscribe(_observer);
        return aggregate.RecordHrv(r.Date, r.Hrv, _actor.ClientId);
    }
}
public sealed class RecordSleepHandler : IRequestHandler<RecordSleepCommand, AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate> _provider;
    private readonly IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly IReadRepository<IDailyMetrics, DailyMetricsReadModel> _read;
    private readonly IDailyMetricsEventPublisher _publisher;
    public RecordSleepHandler(IAggregateProvider<IDailyMetrics, IDailyMetricsAggregate> provider, IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> observer, IActorProvider actor, IReadRepository<IDailyMetrics, DailyMetricsReadModel> read, IDailyMetricsEventPublisher publisher) { _provider = provider; _observer = observer; _actor = actor; _read = read; _publisher = publisher; }
    public async Task<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>> Handle(RecordSleepCommand r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var existing = all.FirstOrDefault(x => x.Date == r.Date && x.ClientId == _actor.ClientId);
        var aggregate = await _provider.GetAggregateAsync(existing?.Id ?? Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>)aggregate).Subscribe(_observer);
        var sleep = Sleep.CreateInstance(r.Sleep.Score, r.Sleep.DurationMin, r.Sleep.DeepMin, r.Sleep.LightMin, r.Sleep.RemMin, r.Sleep.AwakeMin);
        var res = aggregate.RecordSleep(r.Date, sleep, _actor.ClientId);
        if (res.IsSuccess()) { await _publisher.PublishSleepAsync(r.Date, r.Sleep.Score, ct); }
        return res;
    }
}
