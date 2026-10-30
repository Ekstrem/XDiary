using System.Text.Json;
using Xunit;

namespace XDiary.Tests.E2E;

/// <summary>TE-009: E2E соматика + аналитика (срез, сводка, корреляции).</summary>
public class SomaticAnalyticsE2ETests
{
    [E2EFact]
    public async Task SomaticSlice_ReturnsData()
    {
        var c = E2EInfra.Client();
        var token = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, token);

        // контроллер сервиса: /api/v1/somatic/slice (fromT/toT)
        var slice = await E2EInfra.GetAsync(c, "api/v1/somatic-series/slice?fromT=2026-10-29T20:00&toT=2026-10-29T22:00");
        Assert.True(slice.ValueKind == JsonValueKind.Array || slice.ValueKind == JsonValueKind.Object);
    }

    [E2EFact]
    public async Task AnalyticsSummary_ReturnsCards()
    {
        var c = E2EInfra.Client();
        var token = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, token);

        var summary = await E2EInfra.PostAsync(c, "api/v1/analytics/summary", new
        {
            period = "30",
            records = Array.Empty<object>()
        });
        Assert.True(summary.TryGetProperty("sessions", out _));
        Assert.True(summary.TryGetProperty("orgasms", out _));
    }

    [E2EFact]
    public async Task AnalyticsCorrelations_ReturnsList()
    {
        var c = E2EInfra.Client();
        var token = await E2EInfra.LoginAsync(c);
        E2EInfra.Authorized(c, token);

        var corr = await E2EInfra.PostAsync(c, "api/v1/analytics/correlations", new
        {
            records = Array.Empty<object>(),
            metrics = Array.Empty<object>()
        });
        Assert.True(corr.ValueKind == JsonValueKind.Array || corr.ValueKind == JsonValueKind.Object);
    }
}
