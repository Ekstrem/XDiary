namespace SomaticSeries.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using SomaticSeries.Domain;
using SomaticSeries.Domain.Abstraction;
using XDiary.Contracts;

/// <summary>BE-143: наблюдатель Notifier'а только публикует событие; сохранение — отдельный подписчик шины.</summary>
public sealed class BusAdapter : IObserver<AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel>>
{
    private readonly IEventBus _bus;
    public BusAdapter(IEventBus bus) => _bus = bus;

    public void OnNext(AggregateResult<ISomaticSeries, ISomaticSeriesAnemicModel> result)
    {
        if (result.IsSuccess()) _bus.PublishAsync(result.Event);
    }
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}