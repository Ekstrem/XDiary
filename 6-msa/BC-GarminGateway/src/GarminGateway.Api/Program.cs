using Autofac;
using Autofac.Extensions.DependencyInjection;
using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using GarminGateway.Application.Commands;
using GarminGateway.Domain;
using GarminGateway.DomainServices;
using GarminGateway.InternalContracts;
using GarminGateway.Storage;
using GarminGateway.Storage.ReadModel;
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

// РњРµС‚Р°РґР°РЅРЅС‹Рµ Р°РіСЂРµРіР°С‚Р° GarminGateway (in-memory) + С„Р°Р±СЂРёРєР° Р°РіСЂРµРіР°С‚РѕРІ
builder.Services.AddSingleton<IMetadataCache, GarminGatewayMetadataCache>();
builder.Services.AddScoped<AbstractAggregateFactory<IGarminGateway>>();

// Command Store (Event Sourcing, PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("GarminGatewayDb");
builder.Services.AddDbContext<CommandDbContext>(options =>
    options.UseNpgsql(connectionString,
        npgsql => npgsql.MigrationsAssembly("GarminGateway.Storage")));

builder.Services.AddScoped<IQueryRepository<DomainEventEventEntry>, QueryRepository<DomainEventEventEntry>>();
builder.Services.AddScoped<IRepository, Repository>();

// EventBus (InMemory вЂ” РїСЂРѕС„РёР»СЊ Single)
builder.Services.AddEventBusInMemory();

// Read Store (PostgreSQL, С‚Р° Р¶Рµ Р‘Р” вЂ” Р±РµР· РѕС‚РґРµР»СЊРЅРѕР№ СЃС…РµРјС‹)
var readConnectionString = builder.Configuration.GetConnectionString("GarminGatewayReadDb");
if (string.IsNullOrEmpty(readConnectionString))
    readConnectionString = connectionString;
builder.Services.AddReadStorePostgres<IGarminGateway, GarminSyncReadModel, ReadDbContext>(readConnectionString!);

// РџСЂРѕРµРєС†РёРё + Rebuild + РїСЂРѕРµРєС‚РѕСЂ РІ Command Store
builder.Services.AddScoped<GarminGatewayProjectionHandler>();
builder.Services.AddScoped<GarminGatewayCommandDbProjector>();
builder.Services.AddScoped<IRebuildService<IGarminGateway>, GarminGatewayRebuildService>();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(UpsertSyncStateCommand).Assembly));

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "GarminGateway Microservice API",
        Version = "v1",
        Description = "BC-GarminGateway: шлюз-ACL к Garmin (агрегат GarminSync). CEQRS, профиль Single."
    });
});

var app = builder.Build();

// РџРѕРґРїРёСЃР°С‚СЊ РїСЂРѕРµРєС†РёРѕРЅРЅС‹Рµ РѕР±СЂР°Р±РѕС‚С‡РёРєРё РЅР° С€РёРЅСѓ (Event + All РІ РїСЂРѕС„РёР»Рµ Single)
using (var scope = app.Services.CreateScope())
{
    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
    var projectionHandler = scope.ServiceProvider.GetRequiredService<GarminGatewayProjectionHandler>();
    var commandDbProjector = scope.ServiceProvider.GetRequiredService<GarminGatewayCommandDbProjector>();

    eventBus.Subscribe<IGarminGateway>(projectionHandler);
    eventBus.Subscribe<IGarminGateway>(commandDbProjector);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "GarminGateway API v1"));
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
