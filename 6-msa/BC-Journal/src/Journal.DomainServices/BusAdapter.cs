namespace Journal.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using Journal.Domain;
using Journal.Domain.Abstraction;
using XDiary.Contracts;

/// <summary>BE-143: наблюдатель Notifier'а только публикует событие; сохранение — отдельный подписчик шины.</summary>
public sealed class BusAdapter : IObserver<AggregateResult<IJournal, IJournalAnemicModel>>
{
    private readonly IEventBus _bus;
    private readonly AggregateCompletion _completion;
    public BusAdapter(IEventBus bus, AggregateCompletion completion)
    {
        _bus = bus;
        _completion = completion;
    }

    public void OnNext(AggregateResult<IJournal, IJournalAnemicModel> result)
    {
        if (!result.IsSuccess())
        {
            _completion.Succeed(); // события нет — команда не должна зависнуть
            return;
        }
        try
        {
            _ = _bus.PublishAsync(result.Event).ContinueWith(t =>
            {
                if (t.IsFaulted) _completion.Fail(t.Exception!.GetBaseException());
                else _completion.Succeed();
            }, TaskContinuationOptions.ExecuteSynchronously);
        }
        catch (Exception error)
        {
            _completion.Fail(error);
        }
    }
    public void OnError(Exception error) => _completion.Fail(error);
    public void OnCompleted() { }
}