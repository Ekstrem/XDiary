namespace Journal.Domain;

/// <summary>
/// Состояние записи (канвас BC-Journal §2.2/§3): Draft → Recorded → Deleted.
/// </summary>
public enum RecordState
{
    Draft = 0,
    Recorded = 1,
    Deleted = 2,
}
