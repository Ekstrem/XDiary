using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// BE-129 composition-root: IAnemicModelRepository обязан резолвиться в наш
/// JournalEventSourcingRepository (реальный replay через RecordEventReplay), а не в
/// библиотечный FullEventSourcingRepository (GetById = TODO → пустой список → Version=0).
/// </summary>
public class JournalCompositionRootTests
{
    [Fact]
    public void ResolvesRealAnemicRepository_NotLibraryStub()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventStorePostgres<IJournal, IJournalAnemicModel>("Host=localhost;Database=es", null);
        sc.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql("Host=localhost;Database=es", o => o.EnableRetryOnFailure()));
        sc.AddScoped<RecordEventReplay>();
        // BE-149 (CommandRepository.Postgres 0.3.0): доменный rehydrator Full ES.
        sc.AddScoped<DigiTFactory.Libraries.CommandRepository.Postgres.Repositories.IEventStreamRehydrator<IJournal, IJournalAnemicModel>, RecordEventRehydrator>();
        // BE-129: реальный реплей как IAnemicModelRepository (иначе библиотечная заглушка).
        sc.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IJournal, IJournalAnemicModel>, JournalEventSourcingRepository>();

        using var sp = sc.BuildServiceProvider();
        var repo = sp.GetRequiredService<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IJournal, IJournalAnemicModel>>();

        Assert.IsType<JournalEventSourcingRepository>(repo);
    }
}
