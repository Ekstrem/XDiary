namespace Journal.Application;
using Journal.Domain.Abstraction;
public interface IJournalEventPublisher
{
    Task PublishRecordCompletedAsync(IJournalAnemicModel m, CancellationToken ct = default);
    Task PublishRecordDeletedAsync(IJournalAnemicModel m, CancellationToken ct = default);
}