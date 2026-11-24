using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Entities;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.InternalContracts;
using Journal.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Journal.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class JournalApiCollectionDefinition : ICollectionFixture<JournalApiFixture>
{
    public const string Name = "journal-api";
}

/// <summary>Одна одноразовая БД и один self-hosted Journal на весь API-прогон.</summary>
public sealed class JournalApiFixture : IAsyncLifetime, IDisposable
{
    private JournalWebApplicationFactory? _factory;
    private HttpClient? _http;

    public bool Available { get; private set; }
    public string DatabaseName { get; private set; } = string.Empty;
    public JournalWebApplicationFactory Factory => _factory ?? throw new InvalidOperationException("Host is not initialized");
    public HttpClient Http => _http ?? throw new InvalidOperationException("Host is not initialized");
    public JournalClient CreateClient() => CreateClient(Guid.NewGuid());
    public JournalClient CreateClient(Guid clientId) => new(Http, clientId);

    public async Task InitializeAsync()
    {
        if (!JournalTestDatabase.Available) return;
        DatabaseName = JournalTestDatabase.NewDatabaseName();
        await JournalTestDatabase.CreateAsync(DatabaseName);
        _factory = new JournalWebApplicationFactory(DatabaseName);
        _http = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _ = _factory.Services;
        Available = true;
    }

    public async Task DisposeAsync()
    {
        _http?.Dispose();
        if (_factory is not null) await _factory.ShutdownAsync();
        if (Available)
        {
            NpgsqlConnection.ClearAllPools();
            await JournalTestDatabase.DropAsync(DatabaseName);
        }
    }

    public void Dispose() => _http?.Dispose();

    public async Task<JournalReadModel?> ReadRecordAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IReadRepository<IJournal, JournalReadModel>>();
        return await repo.GetByIdAsync(id);
    }

    public async Task<JournalReadModel?> WaitForRecordAsync(Guid id, int timeoutMs = 5000)
    {
        var started = DateTime.UtcNow;
        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            var record = await ReadRecordAsync(id);
            if (record is not null) return record;
            await Task.Delay(100);
        }
        return null;
    }

    public async Task<bool> WaitForAsync(Func<Task<bool>> condition, int timeoutMs = 5000)
    {
        var started = DateTime.UtcNow;
        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            if (await condition()) return true;
            await Task.Delay(100);
        }
        return false;
    }

    public async Task ClearReadStoreAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadDbContext>();
        await db.Set<JournalReadModel>().ExecuteDeleteAsync();
        await db.Set<JournalDefaultsReadModel>().ExecuteDeleteAsync();
        await db.Set<PartnersDirectoryReadModel>().ExecuteDeleteAsync();
    }

    public async Task<IReadOnlyList<JournalReadModel>> AllRecordsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IReadRepository<IJournal, JournalReadModel>>();
        return await repo.GetAllAsync(new Paging(1, int.MaxValue));
    }

    public async Task<JournalDefaultsReadModel?> ReadDefaultsAsync(Guid clientId)
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IReadRepository<IJournal, JournalDefaultsReadModel>>();
        return await repo.GetByIdAsync(clientId);
    }

    public async Task<DomainEventEntry?> EventAsync(Guid id, string commandName)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();
        return await db.Events.Where(e => e.Id == id && e.CommandName == commandName).SingleOrDefaultAsync();
    }
}
