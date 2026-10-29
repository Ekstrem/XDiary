using Autofac;
using Autofac.Extensions.DependencyInjection;
using DigiTFactory.Libraries.AbstractAggregate.Cache;
using DigiTFactory.Libraries.AbstractAggregate.Factory;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using DigiTFactory.Libraries.SeedWorks.Events;
using DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository;
using Privacy.Application.Commands;
using Privacy.Application.Security;
using Privacy.Domain;
using Privacy.DomainServices;
using Privacy.InternalContracts;
using Privacy.Storage;
using Privacy.Storage.ReadModel;
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

// РњРµС‚Р°РґР°РЅРЅС‹Рµ Р°РіСЂРµРіР°С‚Р° PinCredential (in-memory) + С„Р°Р±СЂРёРєР° Р°РіСЂРµРіР°С‚РѕРІ
builder.Services.AddSingleton<IMetadataCache, PrivacyMetadataCache>();
builder.Services.AddScoped<AbstractAggregateFactory<IPrivacy>>();

// Command Store (Event Sourcing, PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("PrivacyDb");
builder.Services.AddDbContext<CommandDbContext>(options =>
    options.UseNpgsql(connectionString,
        npgsql => npgsql.MigrationsAssembly("Privacy.Storage")));

builder.Services.AddScoped<IQueryRepository<DomainEventEventEntry>, QueryRepository<DomainEventEventEntry>>();
builder.Services.AddScoped<IRepository, Repository>();

// EventBus (InMemory вЂ” РїСЂРѕС„РёР»СЊ Single)
builder.Services.AddEventBusInMemory();

// Read Store (PostgreSQL, С‚Р° Р¶Рµ Р‘Р” вЂ” Р±РµР· РѕС‚РґРµР»СЊРЅРѕР№ СЃС…РµРјС‹)
var readConnectionString = builder.Configuration.GetConnectionString("PrivacyReadDb");
if (string.IsNullOrEmpty(readConnectionString))
    readConnectionString = connectionString;
builder.Services.AddReadStorePostgres<IPrivacy, PrivacyReadModel, ReadDbContext>(readConnectionString!);

// РџСЂРѕРµРєС†РёРё + Rebuild + РїСЂРѕРµРєС‚РѕСЂ РІ Command Store
builder.Services.AddScoped<PrivacyProjectionHandler>();
builder.Services.AddScoped<PrivacyCommandDbProjector>();
builder.Services.AddScoped<IRebuildService<IPrivacy>, PrivacyRebuildService>();

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(SetPinCommand).Assembly));

// JWT (HS256) — выпуск/проверка токенов (signing key из конфигурации)
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("xdiary-dev-signing-key-32bytes-min!"));
builder.Services.AddSingleton(new TokenService(jwtSigningKey));

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Privacy Microservice API",
        Version = "v1",
        Description = "BC-Privacy: PIN-доступ (PBKDF2-SHA256) + JWT (агрегат PinCredential). CEQRS, профиль Single."
    });
});

var app = builder.Build();

// РџРѕРґРїРёСЃР°С‚СЊ РїСЂРѕРµРєС†РёРѕРЅРЅС‹Рµ РѕР±СЂР°Р±РѕС‚С‡РёРєРё РЅР° С€РёРЅСѓ (Event + All РІ РїСЂРѕС„РёР»Рµ Single)
using (var scope = app.Services.CreateScope())
{
    var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
    var projectionHandler = scope.ServiceProvider.GetRequiredService<PrivacyProjectionHandler>();
    var commandDbProjector = scope.ServiceProvider.GetRequiredService<PrivacyCommandDbProjector>();

    eventBus.Subscribe<IPrivacy>(projectionHandler);
    eventBus.Subscribe<IPrivacy>(commandDbProjector);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Privacy API v1"));
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.Run();
