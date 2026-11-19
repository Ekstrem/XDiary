using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Partners.Api;
using Partners.Application;
using Xunit;

/// <summary>
/// BE-128: три уровня проверки «ClientId из JWT sub».
/// Уровень 1 — юнит HttpActorProvider (мок IHttpContextAccessor).
/// Уровень 2 — интеграция DI (AddHttpContextAccessor + AddScoped&lt;IActorProvider, HttpActorProvider&gt;).
/// </summary>
public class HttpActorProviderTests
{
    private sealed class StubHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
        public StubHttpContextAccessor(HttpContext? ctx) => HttpContext = ctx;
    }

    [Fact]
    public void ClientId_ReadsSubClaim_FromHttpContext()
    {
        var clientId = Guid.NewGuid();
        var actor = new HttpActorProvider(new StubHttpContextAccessor(new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", clientId.ToString()) }, "test"))
        }));

        Assert.Equal(clientId, actor.ClientId);
    }

    [Fact]
    public void ClientId_NoSub_ReturnsEmpty()
    {
        var actor = new HttpActorProvider(new StubHttpContextAccessor(new DefaultHttpContext()));
        Assert.Equal(Guid.Empty, actor.ClientId);
    }

    [Fact]
    public void DiGraph_ResolvesActor_WithSubFromHttpContext()
    {
        var clientId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddScoped<IActorProvider, HttpActorProvider>();

        using var sp = services.BuildServiceProvider();
        var accessor = sp.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", clientId.ToString()) }, "test"))
        };

        using var scope = sp.CreateScope();
        var actor = scope.ServiceProvider.GetRequiredService<IActorProvider>();

        Assert.IsType<HttpActorProvider>(actor);
        Assert.Equal(clientId, actor.ClientId);
    }
}
