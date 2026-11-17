namespace Journal.Application.Queries;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.InternalContracts;
using MediatR;

public sealed record ListRecordsQuery(Guid ClientId, int? Limit = null, int? Offset = null) : IRequest<IReadOnlyList<JournalReadModel>>;
public sealed record GetRecordByIdQuery(Guid AggregateId) : IRequest<JournalReadModel?>;
public sealed record RecentDefaultsQuery(Guid ClientId) : IRequest<RecentDefaultsView?>;
public sealed record JournalPlacesQuery(Guid ClientId) : IRequest<IReadOnlyList<string>>;
public sealed record JournalFantasiesQuery(Guid ClientId) : IRequest<IReadOnlyList<string>>;

public sealed class ListRecordsHandler : IRequestHandler<ListRecordsQuery, IReadOnlyList<JournalReadModel>>, IQueryHandler
{
    private readonly IReadRepository<IJournal, JournalReadModel> _read;
    private readonly IReadRepository<IJournal, PartnersDirectoryReadModel> _dir;
    public ListRecordsHandler(IReadRepository<IJournal, JournalReadModel> read, IReadRepository<IJournal, PartnersDirectoryReadModel> dir) { _read = read; _dir = dir; }
    public async Task<IReadOnlyList<JournalReadModel>> Handle(ListRecordsQuery r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        IEnumerable<JournalReadModel> q = all.Where(x => x.ClientId == r.ClientId && !x.Deleted); // BE-094: список без удалённых
        if (r.Offset is > 0) q = q.Skip(r.Offset.Value);
        if (r.Limit is > 0) q = q.Take(r.Limit.Value);
        var result = q.ToList();
        await JournalQueryHelper.ResolveNamesAsync(result, _dir, ct);
        return result;
    }
}
public sealed class GetRecordByIdHandler : IRequestHandler<GetRecordByIdQuery, JournalReadModel?>, IQueryHandler
{
    private readonly IReadRepository<IJournal, JournalReadModel> _read;
    private readonly IReadRepository<IJournal, PartnersDirectoryReadModel> _dir;
    public GetRecordByIdHandler(IReadRepository<IJournal, JournalReadModel> read, IReadRepository<IJournal, PartnersDirectoryReadModel> dir) { _read = read; _dir = dir; }
    public async Task<JournalReadModel?> Handle(GetRecordByIdQuery r, CancellationToken ct)
    {
        var x = await _read.GetByIdAsync(r.AggregateId, ct);
        if (x is null || x.Deleted) return null; // BE-094: удалённое → 404
        await JournalQueryHelper.ResolveNamesAsync(new[] { x }, _dir, ct);
        return x;
    }
}
public sealed class RecentDefaultsHandler : IRequestHandler<RecentDefaultsQuery, RecentDefaultsView?>, IQueryHandler
{
    private readonly IReadRepository<IJournal, JournalReadModel> _read;
    private readonly IReadRepository<IJournal, PartnersDirectoryReadModel> _dir;
    public RecentDefaultsHandler(IReadRepository<IJournal, JournalReadModel> read, IReadRepository<IJournal, PartnersDirectoryReadModel> dir) { _read = read; _dir = dir; }
    public async Task<RecentDefaultsView?> Handle(RecentDefaultsQuery r, CancellationToken ct)
    {
        var all = await _read.GetAllAsync(new Paging(1, int.MaxValue), ct);
        var last = all.Where(x => x.ClientId == r.ClientId)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();
        if (last is null) return null;

        // TE-102 (@DO): имя lastPartner резолвим из partners_directory, а не отдаём id.
        string? lastPartnerName = null;
        if (last.Partners.Count > 0 && Guid.TryParse(last.Partners[0], out var pid))
        {
            var directory = await _dir.GetAllAsync(new Paging(1, int.MaxValue), ct);
            // BE-099: фильтр `DeletedAt is null` здесь ОСТАВЛЕН — «последние партнёры» для новой записи.
            lastPartnerName = directory.Where(x => x.DeletedAt is null && x.PartnerId == pid)
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
}
public sealed class JournalPlacesHandler : IRequestHandler<JournalPlacesQuery, IReadOnlyList<string>>, IQueryHandler
{
    private readonly IReadRepository<IJournal, JournalDefaultsReadModel> _defaults;
    public JournalPlacesHandler(IReadRepository<IJournal, JournalDefaultsReadModel> defaults) => _defaults = defaults;
    public async Task<IReadOnlyList<string>> Handle(JournalPlacesQuery r, CancellationToken ct)
        => (await _defaults.GetByIdAsync(r.ClientId, ct))?.Places.ToList() ?? new List<string>();
}
public sealed class JournalFantasiesHandler : IRequestHandler<JournalFantasiesQuery, IReadOnlyList<string>>, IQueryHandler
{
    private readonly IReadRepository<IJournal, JournalDefaultsReadModel> _defaults;
    public JournalFantasiesHandler(IReadRepository<IJournal, JournalDefaultsReadModel> defaults) => _defaults = defaults;
    public async Task<IReadOnlyList<string>> Handle(JournalFantasiesQuery r, CancellationToken ct)
        => (await _defaults.GetByIdAsync(r.ClientId, ct))?.Fantasies.ToList() ?? new List<string>();
}
