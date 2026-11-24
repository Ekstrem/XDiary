namespace SomaticSeries.Application.Commands;
using SomaticSeries.Application;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using MediatR;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using XDiary.Contracts;

public sealed class RecordPointHandler : IRequestHandler<RecordPointCommand, AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>>, ICommandHandler
{
    private readonly IAggregateProvider<ISomaticSeries, ISomaticSeriesAggregate> _provider;
    private readonly IObserver<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>> _observer;
    private readonly IActorProvider _actor;
    private readonly ISomaticSeriesEventPublisher _publisher;
    public RecordPointHandler(IAggregateProvider<ISomaticSeries, ISomaticSeriesAggregate> provider, IObserver<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>> observer, IActorProvider actor, ISomaticSeriesEventPublisher publisher) { _provider = provider; _observer = observer; _actor = actor; _publisher = publisher; }
    public async Task<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>> Handle(RecordPointCommand r, CancellationToken ct)
    {
        var (aggregate, completion) = await ((IAggregateProviderWithCompletion<ISomaticSeries, ISomaticSeriesAggregate>)_provider).GetAggregateWithCompletionAsync(Guid.NewGuid(), ct);
        ((IObservable<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>>)aggregate).Subscribe(_observer);
        var res = aggregate.RecordPoint(r.T, r.Hr, r.BodyBattery, _actor.ClientId, AggregateCommandFactory.Create("RecordPoint"));
        await completion;
        if (res.IsSuccess()) { _ = _publisher.PublishPointAsync(r.T, r.Hr, r.BodyBattery, CancellationToken.None); }
        return res;
    }
}
