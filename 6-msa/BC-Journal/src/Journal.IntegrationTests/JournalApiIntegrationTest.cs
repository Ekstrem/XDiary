using System.Net.Http.Json;
using System.Text.Json;
using Journal.Storage;
using Journal.Storage.ReadModel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Journal.IntegrationTests;

/// <summary>
/// Интеграционный тест: Command → Query → Assert (WebApplicationFactory, профиль Single).
/// Полный визард: StartManualEntry → SpecifyWhen → ChoosePartner → Complete → ListRecords.
/// </summary>
public sealed class JournalApiIntegrationTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public JournalApiIntegrationTest(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:JournalDb",
                Environment.GetEnvironmentVariable("ConnectionStrings__JournalDb")
                ?? "Host=localhost;Port=5432;Database=journal_test;Username=postgres;Password=postgres");
        });

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CommandDbContext>().Database.EnsureCreated();
        scope.ServiceProvider.GetRequiredService<ReadDbContext>().Database.EnsureCreated();
    }

    [Fact]
    public async Task Command_Then_Query_ReturnsRecord()
    {
        var client = _factory.CreateClient();

        // Command: StartManualEntry
        var start = await client.PostAsync("/api/v1/journal/records", null);
        start.EnsureSuccessStatusCode();
        var id = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("aggregateId").GetString()!;

        // Command: SpecifyWhen
        var when = await client.PostAsJsonAsync($"/api/v1/journal/records/{id}/when", new { startedAt = "2026-10-29T21:00" });
        when.EnsureSuccessStatusCode();

        // Command: ChoosePartner
        var partner = await client.PostAsJsonAsync($"/api/v1/journal/records/{id}/partner", new { partnerId = "partner-1" });
        partner.EnsureSuccessStatusCode();

        // Command: Complete
        var complete = await client.PostAsync($"/api/v1/journal/records/{id}/complete", null);
        complete.EnsureSuccessStatusCode();

        // Query: ListRecords → Assert
        var records = await client.GetFromJsonAsync<List<JsonElement>>("/api/v1/journal/records");
        Assert.NotNull(records);
        Assert.Contains(records, r => r.GetProperty("partnerId").GetString() == "partner-1");
    }
}
