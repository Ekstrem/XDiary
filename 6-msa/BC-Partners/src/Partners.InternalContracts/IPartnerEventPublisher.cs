namespace Partners.InternalContracts;

/// <summary>Контракт публикации партнёра в Journal (BE-082). Вынесен в InternalContracts, чтобы command-слой мог публиковать синхронно.</summary>
public interface IPartnerEventPublisher
{
    Task PublishAsync(Guid partnerId, string name, Guid? clientId, DateTime? deletedAt, CancellationToken ct = default);
}
