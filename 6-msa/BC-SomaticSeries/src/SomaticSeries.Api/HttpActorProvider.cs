namespace SomaticSeries.Api;

using Microsoft.AspNetCore.Http;
using SomaticSeries.Application;
using System.Security.Claims;

/// <summary>IActorProvider: владелец запроса из JWT sub (QG-209).</summary>
public sealed class HttpActorProvider : IActorProvider
{
    private readonly IHttpContextAccessor _http;
    public HttpActorProvider(IHttpContextAccessor http) => _http = http;
    public Guid ClientId
    {
        get
        {
            var sub = _http.HttpContext?.User?.FindFirst("sub")?.Value
                      ?? _http.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var g) ? g : Guid.Empty;
        }
    }
}
