namespace Privacy.DomainServices;

/// <summary>BE-145: scoped TCS-мост одной команды. Completion завершается на durable-publish, не на проекции MV.</summary>
public sealed class AggregateCompletion
{
    private TaskCompletionSource<object?>? _current;
    public Task NewTask()
    {
        _current = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _current.Task;
    }
    public void Succeed() => _current?.TrySetResult(null);
    public void Fail(Exception error) => _current?.TrySetException(error);
}