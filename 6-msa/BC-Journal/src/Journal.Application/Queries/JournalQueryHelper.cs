namespace Journal.Application.Queries;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.InternalContracts;

/// <summary>
/// Общая логика query-слоя (без кастомного репозитория — QG-204/302):
/// резолвинг имён партнёров из partners_directory. Хендлеры читают только библиотечные IReadRepository.
/// </summary>
internal static class JournalQueryHelper
{
    public static async Task ResolveNamesAsync(
        IEnumerable<JournalReadModel> models,
        IReadRepository<IJournal, PartnersDirectoryReadModel> dir,
        CancellationToken ct)
    {
        var list = models.ToList();
        if (list.Count == 0) return;
        var directory = await dir.GetAllAsync(new Paging(1, int.MaxValue), ct);
        // BE-099 (@TL): фильтр `DeletedAt is null` СНЯТ намеренно — имя партнёра в истории журнала
        // есть факт прошлого (после мягкого удаления запись остаётся читаемой «с кем»).
        var byId = directory.ToDictionary(x => x.PartnerId, x => x.Name);
        foreach (var m in list)
        {
            m.PartnersNames = m.Partners
                .Select(id => Guid.TryParse(id, out var g) && byId.TryGetValue(g, out var n) ? n : id)
                .ToList();
        }
    }
}
