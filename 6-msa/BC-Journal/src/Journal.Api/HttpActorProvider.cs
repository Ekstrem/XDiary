namespace Journal.Api;

using Journal.Application;

/// <summary>Реализация IActorProvider: достаёт владельца из JWT-клейма sub.</summary>
public sealed class HttpActorProvider : IActorProvider
{
    private readonly IHttpContextAccessor _http;

    public HttpActorProvider(IHttpContextAccessor http) => _http = http;

    public Guid ClientId => Guid.TryParse(_http.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
}
