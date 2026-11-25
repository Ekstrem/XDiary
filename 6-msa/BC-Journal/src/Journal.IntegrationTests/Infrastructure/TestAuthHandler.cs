using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Journal.IntegrationTests.Infrastructure;

/// <summary>Тестовая схема: Authorization: Bearer test:&lt;guid-or-claim&gt;; subject берётся из sub.</summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string TokenPrefix = "test:";
    private const string BearerPrefix = "Bearer ";

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    public static string TokenFor(Guid clientId) => TokenPrefix + clientId.ToString("D");
    public static string AuthorizationFor(Guid clientId) => BearerPrefix + TokenFor(clientId);
    public static string AuthorizationForClaim(string claimValue) => BearerPrefix + TokenPrefix + claimValue;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;
        if (string.IsNullOrWhiteSpace(header)) return Task.FromResult(AuthenticateResult.NoResult());
        if (!header.StartsWith(BearerPrefix, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("Expected Bearer scheme."));
        var token = header[BearerPrefix.Length..].Trim();
        if (!token.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("Expected test: token."));
        var claim = token[TokenPrefix.Length..];
        if (claim.Length == 0) return Task.FromResult(AuthenticateResult.Fail("Empty subject."));
        var identity = new ClaimsIdentity([new Claim("sub", claim)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
