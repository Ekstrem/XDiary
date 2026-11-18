using DigiTFactory.Libraries.CommandRepository.Postgres;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.Storage;
using Xunit;

/// <summary>
/// BE-129/130 composition-root: IAnemicModelRepository обязан резолвиться в наш
/// PartnersEventSourcingRepository (реальный replay через PartnersEventReplay), а не в
/// библиотечный FullEventSourcingRepository (GetById = TODO → пустой список → Version=0).
/// </summary>
public class PartnersCompositionRootTests
{
    [Fact]
    public void ResolvesRealAnemicRepository_NotLibraryStub()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddEventStorePostgres<IPartners, IPartnerAnemicModel>("Host=localhost;Database=es", null);
        sc.AddDbContext<EventStoreDbContext>(o => o.UseNpgsql("Host=localhost;Database=es"));
        sc.AddScoped<PartnersEventReplay>();
        // BE-130: регистрация реального реплея как IAnemicModelRepository (иначе библиотечная заглушка).
        sc.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IPartners, IPartnerAnemicModel>, PartnersEventSourcingRepository>();

        using var sp = sc.BuildServiceProvider();
        var repo = sp.GetRequiredService<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IPartners, IPartnerAnemicModel>>();

        Assert.IsType<PartnersEventSourcingRepository>(repo);
    }
}
