namespace Journal.Storage;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.InternalContracts;
using Microsoft.EntityFrameworkCore;

public sealed class JournalReadRepository : IJournalQueryRepository
{
    private readonly IReadRepository<IJournal, JournalReadModel> _read;
    private readonly IReadRepository<IJournal, PartnersDirectoryReadModel> _dir;
    private readonly ReadDbContext _db; // временно для GetRecent/GetPlaces/GetFantasies (TODO: в MV-проекцию)

    public JournalReadRepository(
        IReadRepository<IJournal, JournalReadModel> read,
        IReadRepository<IJournal, PartnersDirectoryReadModel> dir,
        ReadDbContext db)
    {
        _read = read;
        _dir = dir;
        _db = db;
    }

    private async Task ResolveNamesAsync(IEnumerable<JournalReadModel> models, CancellationToken ct)
    {
        var list = models.ToList();
        if (list.Count == 0) return;
        var dir = await _dir.GetAllAsync(null!, ct);
        var byId = dir.Where(x => x.DeletedAt is null)
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
        var all = await _read.GetAllAsync(null!, ct);
        IEnumerable<JournalReadModel> q = all.Where(x => x.ClientId == clientId);
        if (offset is > 0) q = q.Skip(offset.Value);
        if (limit is > 0) q = q.Take(limit.Value);
        var result = q.ToList();
        await ResolveNamesAsync(result, ct);
        return result;
    }

    public async Task<IReadOnlyList<JournalReadModel>> ListAllAsync(int? limit, int? offset, CancellationToken ct = default)
    {
        var all = await _read.GetAllAsync(null!, ct);
        IEnumerable<JournalReadModel> q = all;
        if (offset is > 0) q = q.Skip(offset.Value);
        if (limit is > 0) q = q.Take(limit.Value);
        var result = q.ToList();
        await ResolveNamesAsync(result, ct);
        return result;
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
        => (int)await _read.CountAsync(ct);

    public async Task<RecentDefaultsView?> GetRecentAsync(Guid clientId, CancellationToken ct = default)
    {
        var all = await _read.GetAllAsync(null!, ct);
        var last = all.Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();
        if (last is null) return null;
        return new RecentDefaultsView
        {
            LastPartner = last.Partners.Count > 0 ? new PartnerRef { Id = last.Partners[0], Name = last.Partners[0] } : null,
            LastPlace = string.IsNullOrWhiteSpace(last.Place) ? null : last.Place,
            LastPositions = last.Positions.ToList(),
            LastTags = last.Tags.ToList(),
            LastContraceptives = last.Contraceptives.ToList(),
            LastFantasies = last.Fantasies.ToList(),
        };
    }

    public async Task<IReadOnlyList<string>> GetPlacesAsync(Guid clientId, CancellationToken ct = default)
    {
        var all = await _read.GetAllAsync(null!, ct);
        return all.Where(x => x.ClientId == clientId && !string.IsNullOrWhiteSpace(x.Place))
            .Select(x => x.Place)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetFantasiesAsync(Guid clientId, CancellationToken ct = default)
    {
        var all = await _read.GetAllAsync(null!, ct);
        return all.Where(x => x.ClientId == clientId)
            .SelectMany(x => x.Fantasies)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }
}
