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
// Профиль деплоймента. Single = роль All (Cmd + Evt + Qry в одном процессе).
// Профили Cqrs/Ceqrs используют роли Command/Event/Query (ADR-002, deployment-profiles).
// ======================================================
var deploymentRole = builder.Configuration["DeploymentRole"] ?? "All";

// Autofac (DomainServices — составление агрегата/Notifier/BusAdapter)
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterModule<RegisterDependencies>();
});

// Command Store + агрегаты — только Command и All
if (deploymentRole is "Command" or "All")
{
    builder.Services.AddSingleton<IMetadataCache, JournalMetadataCache>();
    builder.Services.AddScoped<AbstractAggregateFactory<IJournal>>();

    var connectionString = builder.Configuration.GetConnectionString("JournalDb");
    builder.Services.AddDbContext<CommandDbContext>(options =>
        options.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsAssembly("Journal.Storage")));

    builder.Services.AddScoped<IQueryRepository<DomainEventEventEntry>, QueryRepository<DomainEventEventEntry>>();
    builder.Services.AddScoped<IRepository, Repository>();
}

// EventBus — для Command (publish), Event (subscribe), All. НЕ для Query.
if (deploymentRole is not "Query")
{
    builder.Services.AddEventBusInMemory();
}

// Read Store — для Event, Query и All. НЕ для Command.
if (deploymentRole is "Event" or "Query" or "All")
{
    var connectionString = builder.Configuration.GetConnectionString("JournalDb");
    var readConnectionString = builder.Configuration.GetConnectionString("JournalReadDb");
    if (string.IsNullOrEmpty(readConnectionString))
        readConnectionString = connectionString;
    builder.Services.AddReadStorePostgres<IJournal, JournalReadModel, ReadDbContext>(readConnectionString!);
}

// Проекции + Rebuild — только Event и All
if (deploymentRole is "Event" or "All")
{
    builder.Services.AddScoped<JournalProjectionHandler>();
    builder.Services.AddScoped<JournalCommandDbProjector>();
    builder.Services.AddScoped<IRebuildService<IJournal>, JournalRebuildService>();
}

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

// Подписать проекционные обработчики на шину (Event + All в профиле Single)
if (deploymentRole is "Event" or "All")
{
    using (var scope = app.Services.CreateScope())
    {
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
        var projectionHandler = scope.ServiceProvider.GetRequiredService<JournalProjectionHandler>();
        var commandDbProjector = scope.ServiceProvider.GetRequiredService<JournalCommandDbProjector>();

        eventBus.Subscribe<IJournal>(projectionHandler);
        eventBus.Subscribe<IJournal>(commandDbProjector);
    }
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
