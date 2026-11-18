namespace Privacy.Application.Commands;

using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using MediatR;
using Privacy.Application;
using Privacy.Application.Security;
using Privacy.Domain;
using Privacy.Domain.Abstraction;
using Privacy.InternalContracts;

public sealed record TokenResponse(string AccessToken, string RefreshToken);
public sealed record IssueTokenCommand(string Pin) : IRequest<TokenResponse?>;
public sealed record VerifyTokenQuery(string Token) : IRequest<bool>;

public sealed class IssueTokenHandler : IRequestHandler<IssueTokenCommand, TokenResponse?>, ICommandHandler
{
    private readonly IAggregateProvider<IPrivacy, IPinCredentialAggregate> _provider;
    private readonly IReadRepository<IPrivacy, PinCredentialReadModel> _read;
    private readonly IActorProvider _actor;
    private readonly TokenService _tokens;
    public IssueTokenHandler(IAggregateProvider<IPrivacy, IPinCredentialAggregate> provider, IReadRepository<IPrivacy, PinCredentialReadModel> read, IActorProvider actor, TokenService tokens) { _provider = provider; _read = read; _actor = actor; _tokens = tokens; }

    public async Task<TokenResponse?> Handle(IssueTokenCommand r, CancellationToken ct)
    {
        var m = (await _read.GetAllAsync(new Paging(1, int.MaxValue), ct)).FirstOrDefault(x => x.ClientId == _actor.ClientId && x.SourceId == "pin");
        if (m is null || string.IsNullOrEmpty(m.Hash)) return _tokens.IssueDevice();
        return PinHasher.Verify(r.Pin, m.Salt, m.Hash, m.Iterations) ? _tokens.Issue() : null;
    }
}

public sealed class VerifyTokenHandler : IRequestHandler<VerifyTokenQuery, bool>, IQueryHandler
{
    private readonly TokenService _tokens;
    public VerifyTokenHandler(TokenService tokens) => _tokens = tokens;
    public Task<bool> Handle(VerifyTokenQuery r, CancellationToken ct) => Task.FromResult(_tokens.Verify(r.Token));
}
