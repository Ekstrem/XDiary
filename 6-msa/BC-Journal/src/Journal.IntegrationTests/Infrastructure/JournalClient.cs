using System.Net;
using System.Text;
using System.Text.Json;

namespace Journal.IntegrationTests.Infrastructure;

/// <summary>HTTP-клиент Journal API с owner-token и телами всех 13 команд + 5 запросов.</summary>
public sealed class JournalClient
{
    public const string RecordsRoute = "/api/v1/journal/records";
    public const string JournalRoute = "/api/v1/journal";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public JournalClient(HttpClient http, Guid clientId)
    {
        _http = http;
        ClientId = clientId;
    }

    public Guid ClientId { get; }
    public JournalClient For(Guid clientId) => new(_http, clientId);

    public Task<ApiResponse> SendAsync(HttpMethod method, string path, HttpContent? content = null)
        => SendCoreAsync(method, path, content, TestAuthHandler.AuthorizationFor(ClientId));
    public Task<ApiResponse> SendAnonymousAsync(HttpMethod method, string path, HttpContent? content = null)
        => SendCoreAsync(method, path, content, null);
    public Task<ApiResponse> SendWithAuthorizationAsync(HttpMethod method, string path, string authorization)
        => SendCoreAsync(method, path, null, authorization);
    public Task<ApiResponse> PostRawAsync(string path, string json)
        => SendAsync(HttpMethod.Post, path, new StringContent(json, Encoding.UTF8, "application/json"));
    public Task<ApiResponse> PutAsync(string path) => SendAsync(HttpMethod.Put, path);

    public Task<ApiResponse> StartManualAsync(Guid? id = null)
        => PostAsync(id is null ? RecordsRoute : $"{RecordsRoute}?id={id:D}");
    public Task<ApiResponse> StartFromGarminAsync(string startedAt, int durationMin, string garminId)
        => PostJsonAsync($"{RecordsRoute}/from-garmin", new { StartedAt = startedAt, DurationMin = durationMin, GarminId = garminId });
    public Task<ApiResponse> SpecifyWhenAsync(Guid id, string startedAt)
        => PostJsonAsync($"{RecordsRoute}/{id:D}/when", new { StartedAt = startedAt });
    public Task<ApiResponse> AddPartnerAsync(Guid id, string partnerId)
        => PostJsonAsync($"{RecordsRoute}/{id:D}/partners", new { PartnerId = partnerId });
    public Task<ApiResponse> RemovePartnerAsync(Guid id, string partnerId)
        => PostAsync($"{RecordsRoute}/{id:D}/partners/{partnerId}/remove");
    public Task<ApiResponse> SpecifyPlaceAsync(Guid id, string place)
        => PostJsonAsync($"{RecordsRoute}/{id:D}/place", new { Place = place });
    public Task<ApiResponse> AddPositionsAsync(Guid id, params string[] positions)
        => PostJsonAsync($"{RecordsRoute}/{id:D}/positions", new { Positions = positions });
    public Task<ApiResponse> AddAccessoriesAsync(Guid id, string[] costumes, string[] toys, string[] fantasies, string[] tags)
        => PostJsonAsync($"{RecordsRoute}/{id:D}/accessories", new { Costumes = costumes, Toys = toys, Fantasies = fantasies, Tags = tags });
    public Task<ApiResponse> RecordContraceptionAsync(Guid id, string[] contraceptives, int count)
        => PostJsonAsync($"{RecordsRoute}/{id:D}/contraception", new { Contraceptives = contraceptives, ContraceptiveCount = count });
    public Task<ApiResponse> SpecifyDurationAsync(Guid id, int durationMin)
        => PostJsonAsync($"{RecordsRoute}/{id:D}/duration", new { DurationMin = durationMin });
    public Task<ApiResponse> RecordOrgasmsAsync(Guid id, params object[] orgasms)
        => PostJsonAsync($"{RecordsRoute}/{id:D}/orgasms", new { Orgasms = orgasms });
    public Task<ApiResponse> CompleteAsync(Guid id) => PostAsync($"{RecordsRoute}/{id:D}/complete");
    public Task<ApiResponse> DeleteAsync(Guid id) => PostAsync($"{RecordsRoute}/{id:D}/delete");

    public Task<ApiResponse> GetRecordsAsync(string? query = null)
        => SendAsync(HttpMethod.Get, string.IsNullOrEmpty(query) ? RecordsRoute : $"{RecordsRoute}?{query}");
    public Task<ApiResponse> GetRecordAsync(Guid id) => SendAsync(HttpMethod.Get, $"{RecordsRoute}/{id:D}");
    public Task<ApiResponse> GetRecentAsync() => SendAsync(HttpMethod.Get, $"{JournalRoute}/recent");
    public Task<ApiResponse> GetPlacesAsync() => SendAsync(HttpMethod.Get, $"{JournalRoute}/places");
    public Task<ApiResponse> GetFantasiesAsync() => SendAsync(HttpMethod.Get, $"{JournalRoute}/fantasies");

    private Task<ApiResponse> PostAsync(string path) => SendAsync(HttpMethod.Post, path);
    private Task<ApiResponse> PostJsonAsync<T>(string path, T body)
        => SendAsync(HttpMethod.Post, path, new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json"));

    private async Task<ApiResponse> SendCoreAsync(HttpMethod method, string path, HttpContent? content, string? authorization)
    {
        using var request = new HttpRequestMessage(method, path);
        if (content is not null) request.Content = content;
        if (authorization is not null) request.Headers.TryAddWithoutValidation("Authorization", authorization);
        using var response = await _http.SendAsync(request);
        return new ApiResponse(response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
