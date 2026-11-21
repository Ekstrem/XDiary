namespace DailyMetrics.DomainServices;

using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using XDiary.Contracts;

/// <summary>BE-145: только PublishAsync; scoped completion завершается на durable-publish, не на проекции MV.</summary>
public sealed class BusAdapter : IObserver<AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel>>
{
    private readonly IEventBus _bus;
    private readonly AggregateCompletion _completion;
    public BusAdapter(IEventBus bus, AggregateCompletion completion)
    {
        _bus = bus;
        _completion = completion;
    }

    public void OnNext(AggregateResult<IDailyMetrics, IDailyMetricsAnemicModel> result)
    {
        if (!result.IsSuccess())
        {
            _completion.Succeed();
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