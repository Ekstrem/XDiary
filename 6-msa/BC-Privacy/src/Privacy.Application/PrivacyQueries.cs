namespace Privacy.Application.Queries;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using Privacy.Application.Security;
using Privacy.Domain;
using Privacy.InternalContracts;
public sealed record HasPinQuery(Guid ClientId) : IRequest<bool>;
public sealed record VerifyPinQuery(Guid ClientId, string Pin) : IRequest<bool>;

public sealed class HasPinHandler : IRequestHandler<HasPinQuery, bool>, IQueryHandler
{
    private readonly IReadRepository<IPrivacy, PinCredentialReadModel> _read;
    public HasPinHandler(IReadRepository<IPrivacy, PinCredentialReadModel> read) => _read = read;
    public async Task<bool> Handle(HasPinQuery r, CancellationToken ct)
        => (await _read.GetAllAsync(new Paging(1, int.MaxValue), ct)).Any(x => x.ClientId == r.ClientId && x.SourceId == "pin" && !string.IsNullOrEmpty(x.Hash));
}
public sealed class VerifyPinHandler : IRequestHandler<VerifyPinQuery, bool>, IQueryHandler
{
    private readonly IReadRepository<IPrivacy, PinCredentialReadModel> _read;
    public VerifyPinHandler(IReadRepository<IPrivacy, PinCredentialReadModel> read) => _read = read;
    public async Task<bool> Handle(VerifyPinQuery r, CancellationToken ct)
    {
        var m = (await _read.GetAllAsync(new Paging(1, int.MaxValue), ct)).FirstOrDefault(x => x.ClientId == r.ClientId && x.SourceId == "pin");
        if (m is null || string.IsNullOrEmpty(m.Hash)) return false;
        return PinHasher.Verify(r.Pin, m.Salt, m.Hash, m.Iterations);
    }
}
