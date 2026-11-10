namespace Journal.InternalContracts;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;

/// <summary>
/// Справочник имён партнёров (проекция §2.7) для разрешения partnerId → имя
/// в ответах Journal (аддитивно к списку id).
/// </summary>
public sealed class PartnersDirectoryReadModel : IReadModel<Journal.Domain.IJournal>
{
    // DF PostgresReadModelStore.UpsertAsync ищет ключ по имени "Id" (@DO 14:28);
    // колонка в БД остаётся "PartnerId" (HasColumnName), PartnerId — обратная совместимость.
    public Guid Id { get; set; }
    public Guid PartnerId => Id;
    public string Name { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public DateTime? DeletedAt { get; set; }
}
