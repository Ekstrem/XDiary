using System.Net;
using System.Text.Json;
using Xunit;

namespace Journal.IntegrationTests.Infrastructure;

public sealed record ApiResponse(HttpStatusCode StatusCode, string Body)
{
    public JsonDocument Json() => JsonDocument.Parse(Body);
    public T? ReadJson<T>() => JsonSerializer.Deserialize<T>(Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    public ApiResponse EnsureOk(string? step = null)
    {
        Assert.True(StatusCode is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.NoContent,
            $"Expected success{(step is null ? "" : $" at {step}")}, got {(int)StatusCode}: {Body}");
        return this;
    }
    public ApiResponse Ensure(HttpStatusCode expected)
    {
        Assert.Equal(expected, StatusCode);
        return this;
    }
}
