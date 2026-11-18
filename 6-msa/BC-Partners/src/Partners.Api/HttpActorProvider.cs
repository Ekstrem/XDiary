namespace Partners.Api;

using Microsoft.AspNetCore.Http;
using Partners.Application;
using System.Security.Claims;

/// <summary>IActorProvider: владелец запроса из JWT sub (не из тела команды — QG-209).</summary>
public sealed class HttpActorProvider : IActorProvider
{
    private readonly IHttpContextAccessor _http;
    public HttpActorProvider(IHttpContextAccessor http) => _http = http;

    public Guid ClientId
    {
        get
        {
            var ctx = _http.HttpContext;
            if (ctx is not null && ctx.Items.TryGetValue("ActorClientId", out var v) && v is Guid gv)
                return gv;
            var user = ctx?.User;
            var sub = user?.FindFirst("sub")?.Value
                      ?? user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user?.FindFirst("nameidentifier")?.Value
                      ?? user?.FindFirst("nameid")?.Value;
            return Guid.TryParse(sub, out var g) ? g : Guid.Empty;
        }
    }
}
