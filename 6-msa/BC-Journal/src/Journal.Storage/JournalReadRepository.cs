namespace Journal.Storage;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.InternalContracts;
using Microsoft.EntityFrameworkCore;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;

public sealed record Paging(int Page, int PageSize) : IPaging;

public sealed class JournalReadRepository : IJournalQueryRepository
{
    private readonly IReadRepository<IJournal, JournalReadModel> _read;
    private readonly IReadRepository<IJournal, PartnersDirectoryReadModel> _dir;
    private readonly IReadRepository<IJournal, JournalDefaultsReadModel> _defaults;
    private readonly ReadDbContext _db; // временно для GetRecent (TODO: в MV-проекцию)

    public JournalReadRepository(
        IReadRepository<IJournal, JournalReadModel> read,
        IReadRepository<IJournal, PartnersDirectoryReadModel> dir,
        IReadRepository<IJournal, JournalDefaultsReadModel> defaults,
        ReadDbContext db)
    {
        _read = read;
        _dir = dir;
        _defaults = defaults;
        _db = db;
    }

    private async Task ResolveNamesAsync(IEnumerable<JournalReadModel> models, CancellationToken ct)
    {
        var list = models.ToList();
        if (list.Count == 0) return;
        var dir = await _dir.GetAllAsync(new Paging(1, int.MaxValue), ct);
        // BE-099 (@TL): фильтр `DeletedAt is null` здесь СНЯТ намеренно — имя партнёра
        // в ИСТОРИИ журнала есть факт прошлого: после мягкого удаления запись остаётся
        // читаемой («с кем»), иначе `TE-050` краснеет (`Remove_PartnerInExistingJournalRecord_StillResolvable`).
        // НЕ унифицировать с `GetRecentAsync` ниже: там фильтр обязателен.
        var byId = dir
            .ToDictionary(x => x.PartnerId, x => x.Name);
        foreach (var m in list)
        {
            m.PartnersNames = m.Partners
                .Select(id => Guid.TryParse(id, out var g) && byId.TryGetValue(g, out var n) ? n : id)
                .ToList();
        }
    }

    public async Task<JournalReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var m = await _read.GetByIdAsync(id, ct);
        if (m is not null) await ResolveNamesAsync(new[] { m }, ct);
        return m;
    }

    public async Task<IReadOnlyList<JournalReadModel>> ListAsync(Guid clientId, int? limit, int? offset, CancellationToken ct = default)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        IEnumerable<JournalReadModel> q = all.Where(x => x.ClientId == clientId);
        if (offset is > 0) q = q.Skip(offset.Value);
        if (limit is > 0) q = q.Take(limit.Value);
        var result = q.ToList();
        await ResolveNamesAsync(result, ct);
        return result;
    }

    public async Task<IReadOnlyList<JournalReadModel>> ListAllAsync(int? limit, int? offset, CancellationToken ct = default)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        IEnumerable<JournalReadModel> q = all;
        if (offset is > 0) q = q.Skip(offset.Value);
        if (limit is > 0) q = q.Take(limit.Value);
        var result = q.ToList();
        await ResolveNamesAsync(result, ct);
        return result;
    }

    public async Task<IReadOnlyList<Guid>> GetClientIdsAsync(CancellationToken ct = default)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        return all.Where(x => x.ClientId is not null).Select(x => x.ClientId!.Value).Distinct().ToList();
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
        => (int)await _read.CountAsync(ct);

    public async Task<RecentDefaultsView?> GetRecentAsync(Guid clientId, CancellationToken ct = default)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var last = all.Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();
        if (last is null) return null;

        // TE-102 (@DO 11:59): имя lastPartner резолвим из partners_directory, а не отдаём id.
        string? lastPartnerName = null;
        if (last.Partners.Count > 0 && Guid.TryParse(last.Partners[0], out var pid))
        {
            var dir = await _dir.GetAllAsync(new Paging(1, int.MaxValue), ct);
            // BE-099 (@TL): фильтр `DeletedAt is null` здесь ОСТАВЛЕН намеренно — это
            // «последние партнёры» для НОВОЙ записи, то есть выбор из ДОСТУПНЫХ сейчас.
            // Снятие фильтра вернуло бы удалённого партнёра в подсказки (регрессия).
            lastPartnerName = dir.Where(x => x.DeletedAt is null && x.PartnerId == pid)
                .Select(x => x.Name)
                .FirstOrDefault();
        }

        return new RecentDefaultsView
        {
            LastPartner = last.Partners.Count > 0 ? new PartnerRef { Id = last.Partners[0], Name = lastPartnerName ?? last.Partners[0] } : null,
            LastPlace = string.IsNullOrWhiteSpace(last.Place) ? null : last.Place,
            LastPositions = last.Positions.ToList(),
            LastTags = last.Tags.ToList(),
            LastContraceptives = last.Contraceptives.ToList(),
            LastFantasies = last.Fantasies.ToList(),
        };
    }

    public async Task<IReadOnlyList<string>> GetPlacesAsync(Guid clientId, CancellationToken ct = default)
    {
        var d = await _defaults.GetByIdAsync(clientId, ct);
        return d?.Places.ToList() ?? new List<string>();
    }

    public async Task<IReadOnlyList<string>> GetFantasiesAsync(Guid clientId, CancellationToken ct = default)
    {
        var d = await _defaults.GetByIdAsync(clientId, ct);
        return d?.Fantasies.ToList() ?? new List<string>();
    }
}
