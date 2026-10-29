namespace Portability.Storage;

/// <summary>
/// Р—Р°РїРёСЃСЊ РґРѕРјРµРЅРЅРѕРіРѕ СЃРѕР±С‹С‚РёСЏ РІ Command Store.
/// </summary>
public class DomainEventEventEntry
{
    public Guid Id { get; set; }
    public long Version { get; set; }
    public Guid CorrelationToken { get; set; }
    public string CommandName { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;

    /// <summary>РЎРµСЂРёР°Р»РёР·РѕРІР°РЅРЅС‹Рµ ChangedValueObjects (JSON).</summary>
    public string ChangedValueObjectsJson { get; set; } = string.Empty;

    public string BoundedContext { get; set; } = string.Empty;
    public int Result { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
