using Autofac;
using Autofac.Extensions.DependencyInjection;
using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Journal.Application.Commands;
using Journal.Domain;
using Journal.DomainServices;
using Journal.InternalContracts;
using Journal.Storage;
using Journal.Storage.ReadModel;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// РџСЂРѕС„РёР»СЊ Single (ADR-002): 1 РїСЂРѕС†РµСЃСЃ (Cmd + Evt + Qry).
// Command DB = PostgreSQL (Event Sourcing), Read DB = С‚Р° Р¶Рµ Р‘Р”,
// EventBus = InMemory.
// ======================================================

// Autofac
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterModule<RegisterDependencies>();
});

// РњРµС‚Р°РґР°РЅРЅС‹Рµ Р°РіСЂРµРіР°С‚Р° Record (in-memory) + С„Р°Р±СЂРёРєР° Р°РіСЂРµРіР°С‚РѕРІ
builder.Services.AddSingleton<IMetadataCache, JournalMetadataCache>();
builder.Services.AddScoped<AbstractAggregateFactory<IJournal>>();

// Command Store (Event Sourcing, PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("JournalDb");
builder.Services.AddDbContext<CommandDbContext>(options =>
    options.UseNpgsql(connectionString,
        npgsql => npgsql.MigrationsAssembly("Journal.Storage")));

builder.Services.AddScoped<IQueryRepository<DomainEventEventEntry>, QueryRepository<DomainEventEventEntry>>();
builder.Services.AddScoped<IRepository, Repository>();

// EventBus (InMemory вЂ” РїСЂРѕС„РёР»СЊ Single)
builder.Services.AddEventBusInMemory();

// Read Store (PostgreSQL, С‚Р° Р¶Рµ Р‘Р” вЂ” Р±РµР· РѕС‚РґРµР»СЊРЅРѕР№ СЃС…РµРјС‹)
var readConnectionString = builder.Configuration.GetConnectionString("JournalReadDb");
if (string.IsNullOrEmpty(readConnectionString))
    readConnectionString = connectionString;
builder.Services.AddReadStorePostgres<IJournal, JournalReadModel, ReadDbContext>(readConnectionString!);

// РџСЂРѕРµРєС†РёРё + Rebuild + РїСЂРѕРµРєС‚РѕСЂ РІ Command Store
builder.Services.AddScoped<JournalProjectionHandler>();
builder.Services.AddScoped<JournalCommandDbProjector>();
builder.Services.AddScoped<IRebuildService<IJournal>, JournalRebuildService>();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(StartManualEntryCommand).Assembly));

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Journal Microservice API",
        Version = "v1",
        Description = "BC-Journal: журнал интимной активности (агрегат Record, визард). CEQRS, профиль Single."
    });
});

var app = builder.Build();

// РџРѕРґРїРёСЃР°С‚СЊ РїСЂРѕРµРєС†РёРѕРЅРЅС‹Рµ РѕР±СЂР°Р±РѕС‚С‡РёРєРё РЅР° С€РёРЅСѓ (Event + All РІ РїСЂРѕС„РёР»Рµ Single)
using (var scope = app.Services.CreateScope())
{
    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
    var projectionHandler = scope.ServiceProvider.GetRequiredService<JournalProjectionHandler>();
    var commandDbProjector = scope.ServiceProvider.GetRequiredService<JournalCommandDbProjector>();

    eventBus.Subscribe<IJournal>(projectionHandler);
    eventBus.Subscribe<IJournal>(commandDbProjector);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Journal API v1"));
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Маркер для WebApplicationFactory в интеграционных тестах.</summary>
public partial class Program { }
