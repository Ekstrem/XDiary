using Autofac;
using Autofac.Extensions.DependencyInjection;
using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using AppleHealthGateway.Application.Commands;
using AppleHealthGateway.Domain;
using AppleHealthGateway.DomainServices;
using AppleHealthGateway.InternalContracts;
using AppleHealthGateway.Storage;
using AppleHealthGateway.Storage.ReadModel;
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

// РњРµС‚Р°РґР°РЅРЅС‹Рµ Р°РіСЂРµРіР°С‚Р° AppleHealthGateway (in-memory) + С„Р°Р±СЂРёРєР° Р°РіСЂРµРіР°С‚РѕРІ
builder.Services.AddSingleton<IMetadataCache, AppleHealthGatewayMetadataCache>();
builder.Services.AddScoped<AbstractAggregateFactory<IAppleHealthGateway>>();

// Command Store (Event Sourcing, PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("AppleHealthGatewayDb");
builder.Services.AddDbContext<CommandDbContext>(options =>
    options.UseNpgsql(connectionString,
        npgsql => npgsql.MigrationsAssembly("AppleHealthGateway.Storage")));

builder.Services.AddScoped<IQueryRepository<DomainEventEventEntry>, QueryRepository<DomainEventEventEntry>>();
builder.Services.AddScoped<IRepository, Repository>();

// EventBus (InMemory вЂ” РїСЂРѕС„РёР»СЊ Single)
builder.Services.AddEventBusInMemory();

// Read Store (PostgreSQL, С‚Р° Р¶Рµ Р‘Р” вЂ” Р±РµР· РѕС‚РґРµР»СЊРЅРѕР№ СЃС…РµРјС‹)
var readConnectionString = builder.Configuration.GetConnectionString("AppleHealthGatewayReadDb");
if (string.IsNullOrEmpty(readConnectionString))
    readConnectionString = connectionString;
builder.Services.AddReadStorePostgres<IAppleHealthGateway, AppleImportReadModel, ReadDbContext>(readConnectionString!);

// РџСЂРѕРµРєС†РёРё + Rebuild + РїСЂРѕРµРєС‚РѕСЂ РІ Command Store
builder.Services.AddScoped<AppleHealthGatewayProjectionHandler>();
builder.Services.AddScoped<AppleHealthGatewayCommandDbProjector>();
builder.Services.AddScoped<IRebuildService<IAppleHealthGateway>, AppleHealthGatewayRebuildService>();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(UpsertImportStateCommand).Assembly));

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AppleHealthGateway Microservice API",
        Version = "v1",
        Description = "BC-AppleHealthGateway: шлюз-ACL к Apple Health (агрегат AppleImport). CEQRS, профиль Single."
    });
});

var app = builder.Build();

// РџРѕРґРїРёСЃР°С‚СЊ РїСЂРѕРµРєС†РёРѕРЅРЅС‹Рµ РѕР±СЂР°Р±РѕС‚С‡РёРєРё РЅР° С€РёРЅСѓ (Event + All РІ РїСЂРѕС„РёР»Рµ Single)
using (var scope = app.Services.CreateScope())
{
    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
    var projectionHandler = scope.ServiceProvider.GetRequiredService<AppleHealthGatewayProjectionHandler>();
    var commandDbProjector = scope.ServiceProvider.GetRequiredService<AppleHealthGatewayCommandDbProjector>();

    eventBus.Subscribe<IAppleHealthGateway>(projectionHandler);
    eventBus.Subscribe<IAppleHealthGateway>(commandDbProjector);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AppleHealthGateway API v1"));
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
