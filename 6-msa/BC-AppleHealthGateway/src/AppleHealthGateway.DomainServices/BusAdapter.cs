namespace AppleHealthGateway.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using XDiary.Contracts;

/// <summary>BE-143: наблюдатель Notifier'а только публикует событие; сохранение — отдельный подписчик шины.</summary>
public sealed class BusAdapter : IObserver<AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>>
{
    private readonly IEventBus _bus;
    public BusAdapter(IEventBus bus) => _bus = bus;

    public void OnNext(AggregateResult<IAppleHealthGateway, IAppleHealthGatewayAnemicModel> result)
    {
        if (result.IsSuccess()) _bus.PublishAsync(result.Event);
    }
    public void OnError(Exception error) { }
    public void OnCompleted() { }
}