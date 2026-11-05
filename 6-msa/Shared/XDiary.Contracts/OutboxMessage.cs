namespace XDiary.Contracts;

/// <summary>
/// BE-059 (E5): строка transactional outbox. Публикация события пишется в эту таблицу
/// в той же транзакции, что и командное изменение; доставку делает фоновый воркер
/// (хотя бы один раз, идемпотентно по Id).
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Имя типа события/контекста для восстановления обработчика.</summary>
    public string EventType { get; set; } = string.Empty;
    /// <summary>Сериализованный домен-событие (JSON).</summary>
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>null = не доставлено; не-null = доставлено (идемпотентность).</summary>
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
}
