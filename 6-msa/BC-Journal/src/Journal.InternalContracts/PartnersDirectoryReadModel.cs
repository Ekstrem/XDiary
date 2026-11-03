namespace Journal.InternalContracts;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>
/// Справочник имён партнёров (проекция §2.7) для разрешения partnerId → имя
/// в ответах Journal (аддитивно к списку id).
/// </summary>
public sealed class PartnersDirectoryReadModel : IReadModel<Journal.Domain.IJournal>
{
    public Guid PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public DateTime? DeletedAt { get; set; }
}
