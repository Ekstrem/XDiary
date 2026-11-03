namespace Partners.Storage;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Partners.Domain;
using Partners.InternalContracts;

public sealed class PartnerReadRepository : IPartnersQueryRepository
{
    private readonly IReadRepository<IPartners, PartnersReadModel> _read;
    public PartnerReadRepository(IReadRepository<IPartners, PartnersReadModel> read) => _read = read;

    public async Task<PartnersReadModel?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _read.GetByIdAsync(id, ct);

    public async Task<IReadOnlyList<PartnersReadModel>> ListAsync(Guid clientId, CancellationToken ct = default)
    {
        var all = await _read.GetAllAsync(null!, ct);
        return all.Where(x => x.ClientId == clientId).ToList();
    }

    public async Task<int> CountAsync(CancellationToken ct = default)
        => (int)await _read.CountAsync(ct);
}
