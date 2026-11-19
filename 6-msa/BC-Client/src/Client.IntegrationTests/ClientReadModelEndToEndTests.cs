using Client.Api.Events;
using Client.Application.Abstraction;
using Client.Application.Commands;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.Domain.Implementation;
using Client.DomainServices;
using Client.InternalContracts;
using Client.Storage;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.Result;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XDiary.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace Client.IntegrationTests;

/// <summary>
/// BE-140 L4: сквозная проводка `command → event → bus → projection → read` для Client на РЕАЛЬНОМ Postgres.
/// </summary>
public class ClientReadModelEndToEndTests
{
    private const string Phone = "+79000000000";
    private readonly ITestOutputHelper _out;
    public ClientReadModelEndToEndTests(ITestOutputHelper output) => _out = output;

    [Trait("Requires", "Postgres")]
    [Fact]
    public async Task Register_Then_Read_FromDb()
    {
        var cs = Environment.GetEnvironmentVariable("XDIARY_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(cs))
        {
            _out.WriteLine("SKIPPED: XDIARY_TEST_POSTGRES не задан — e2e работает только в CI.");
            return;
        }

        var options = new DbContextOptionsBuilder<ReadDbContext>().UseNpgsql(cs, o => o.EnableRetryOnFailure()).Options;
        using (var migrate = new ReadDbContext(options)) migrate.Database.Migrate();

        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventBusInMemory();
        sc.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(
            o => o.UseNpgsql(cs, x => x.MigrationsAssembly("Client.Storage").EnableRetryOnFailure()));
        sc.AddEventStorePostgres<IClients, IClientAnemicModel>(cs, null);
        sc.AddScoped<IEventStoreRepository<IClients, IClientAnemicModel>, ClientEventSourcingRepository>();
        sc.AddScoped<IAnemicModelRepository<IClients, IClientAnemicModel>, ClientEventSourcingRepository>();
        sc.AddScoped<ClientEventReplay>();
        sc.AddReadStorePostgres<IClients, ClientReadModel, ReadDbContext>(cs, null);
        sc.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
        sc.AddScoped<IAggregateProvider<IClients, IClientAggregate>, AggregateProvider>();
        sc.AddScoped<IObserver<AggregateResult<IClients, IClientAnemicModel>>, BusAdapter>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        sc.AddSingleton<IConfiguration>(config);
        sc.AddSingleton<IOtpSender, FakeOtpSender>();
        sc.AddSingleton<IOtpRateLimiter, FakeRateLimiter>();
        sc.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        using var sp = sc.BuildServiceProvider();

        var handler = new ClientEventHandler(sp.GetRequiredService<IServiceScopeFactory>());
        sp.GetRequiredService<IEventBusConsumer>().Subscribe(handler);

        var cmd = new RegisterClientHandler(
            sp.GetRequiredService<IAggregateProvider<IClients, IClientAggregate>>(),
            sp.GetRequiredService<IObserver<AggregateResult<IClients, IClientAnemicModel>>>(),
            sp.GetRequiredService<IReadRepository<IClients, ClientReadModel>>(),
            sp.GetRequiredService<IOtpSender>(),
            sp.GetRequiredService<IOtpRateLimiter>(),
            config,
            sp.GetRequiredService<ILogger<RegisterClientHandler>>());
        var res = await cmd.Handle(new RegisterClientCommand(Phone), default);
        Assert.True(res.Result.IsSuccess());

        await Task.Delay(700);

        var repo = sp.GetRequiredService<IReadRepository<IClients, ClientReadModel>>();
        var stored = await repo.GetByIdAsync(res.Result.Model().Id, default);
        Assert.NotNull(stored);
        Assert.Equal(Phone, stored!.Phone);
    }

    private sealed class FakeOtpSender : IOtpSender
    {
        public Task<bool> SendAsync(string phone, string code, CancellationToken ct = default) => Task.FromResult(true);
    }

    private sealed class FakeRateLimiter : IOtpRateLimiter
    {
        public bool TryAcquire(string key, DateTime now) => true;
    }
}
