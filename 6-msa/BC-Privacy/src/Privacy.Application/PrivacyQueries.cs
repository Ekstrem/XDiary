namespace Privacy.Application.Queries;
using MediatR;
using Privacy.Application.Security;
using Privacy.InternalContracts;
public sealed record HasPinQuery : IRequest<bool>;
public sealed record VerifyPinQuery(string Pin) : IRequest<bool>;

public sealed class HasPinHandler : IRequestHandler<HasPinQuery, bool>
{
    private readonly IPinCredentialQueryRepository _repo;
    public HasPinHandler(IPinCredentialQueryRepository repo) => _repo = repo;
    public async Task<bool> Handle(HasPinQuery r, CancellationToken ct) => (await _repo.GetBySourceIdAsync("pin", ct))?.HasPin ?? false;
}
public sealed class VerifyPinHandler : IRequestHandler<VerifyPinQuery, bool>
{
    private readonly IPinCredentialQueryRepository _repo;
    public VerifyPinHandler(IPinCredentialQueryRepository repo) => _repo = repo;
    public async Task<bool> Handle(VerifyPinQuery r, CancellationToken ct)
    {
        var m = await _repo.GetBySourceIdAsync("pin", ct);
        if (m is null || !m.HasPin) return false;
        return PinHasher.Verify(r.Pin, m.Salt, m.Hash, m.Iterations);
    }
}