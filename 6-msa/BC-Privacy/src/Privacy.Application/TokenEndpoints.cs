namespace Privacy.Application.Commands;

using MediatR;
using Privacy.Application.Security;
using Privacy.InternalContracts;

public sealed record TokenResponse(string AccessToken, string RefreshToken);
public sealed record IssueTokenCommand(string Pin) : IRequest<TokenResponse?>;
public sealed record VerifyTokenQuery(string Token) : IRequest<bool>;

public sealed class IssueTokenHandler : IRequestHandler<IssueTokenCommand, TokenResponse?>
{
    private readonly IPinCredentialQueryRepository _repo;
    private readonly TokenService _tokens;
    public IssueTokenHandler(IPinCredentialQueryRepository repo, TokenService tokens) { _repo = repo; _tokens = tokens; }

    public async Task<TokenResponse?> Handle(IssueTokenCommand r, CancellationToken ct)
    {
        var m = await _repo.GetBySourceIdAsync("pin", ct);
        if (m is null || !m.HasPin) return null;
        return PinHasher.Verify(r.Pin, m.Salt, m.Hash, m.Iterations) ? _tokens.Issue() : null;
    }
}

public sealed class VerifyTokenHandler : IRequestHandler<VerifyTokenQuery, bool>
{
    private readonly TokenService _tokens;
    public VerifyTokenHandler(TokenService tokens) => _tokens = tokens;
    public Task<bool> Handle(VerifyTokenQuery r, CancellationToken ct) => Task.FromResult(_tokens.Verify(r.Token));
}