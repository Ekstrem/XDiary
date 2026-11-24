using Autofac;
using Autofac.Extensions.DependencyInjection;
using Journal.Application;
using Journal.Domain;
using Journal.Domain.Abstraction;
using Journal.DomainServices;
using Journal.InternalContracts;
using Journal.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Journal.Api.Events;
using Journal.Api.HealthChecks;
using XDiary.Contracts;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;

var builder = WebApplication.CreateBuilder(args);
// BE-151: self-hosted integration tests не пишут в Windows EventLog (песочница запрещает).
if (Environment.GetEnvironmentVariable("JOURNAL_TESTS") == "true")
{
    builder.Logging.ClearProviders();
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
}
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(c => c.RegisterModule<RegisterDependencies>());
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "xdiary",
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(XDiary.Contracts.JwtSigningKey.Resolve(builder.Configuration["Jwt:SigningKey"], builder.Environment.IsDevelopment()))
    });
builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Journal.Application.Commands.StartManualEntryCommand>());
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("CommandDb"))) throw new InvalidOperationException("ConnectionStrings:CommandDb РЅРµ Р·Р°РґР°РЅ");
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("ReadDb"))) throw new InvalidOperationException("ConnectionStrings:ReadDb РЅРµ Р·Р°РґР°РЅ");
// BE-075 (РїРѕСЂСЏРґРѕРє РІР°Р¶РµРЅ, РќР• РјРµРЅСЏС‚СЊ): РїРµСЂРІР°СЏ СЂРµРіРёСЃС‚СЂР°С†РёСЏ DbContextOptions РІС‹РёРіСЂС‹РІР°РµС‚ (TryAdd),
// РїРѕСЌС‚РѕРјСѓ AddDbContext СЃ MigrationsAssembly РѕР±СЏР·Р°РЅ РёРґС‚Рё Р”Рћ AddEventStorePostgres вЂ” РёРЅР°С‡Рµ
// DF-РїР°РєРµС‚ РїРµСЂРµРєСЂРѕРµС‚ MigrationsAssembly Рё Migrate() СѓРІРёРґРёС‚ РїСѓСЃС‚РѕР№ РЅР°Р±РѕСЂ (42P01). Р”РѕРєР°Р·Р°РЅРѕ РЅР° dev: de5507f0.
builder.Services.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb"), x => x.MigrationsAssembly("Journal.Storage").EnableRetryOnFailure()));
builder.Services.AddEventStorePostgres<IJournal, IJournalAnemicModel>(builder.Configuration.GetConnectionString("CommandDb") ?? "", null);
// QG-301/override-СЂРµРїР»РµР№: РїРµСЂРµРєР»СЋС‡Р°РµРј IEventStoreRepository РЅР° РЅР°СЃР»РµРґРЅРёРєР° СЃ СЂРµР°Р»СЊРЅС‹Рј fold (RecordEventReplay),
// РёРЅР°С‡Рµ FullEventSourcingRepository.GetById РІРѕР·РІСЂР°С‰Р°РµС‚ РїСѓСЃС‚РѕР№ СЃРїРёСЃРѕРє (TODO РІ Р±РёР±Р»РёРѕС‚РµРєРµ).
builder.Services.AddScoped<DigiTFactory.Libraries.CommandRepository.Postgres.Repositories.IEventStoreRepository<IJournal, IJournalAnemicModel>, Journal.Storage.JournalEventSourcingRepository>();
// BE-129: реальный реплей и как IAnemicModelRepository — иначе AggregateProvider берёт библиотечный
// FullEventSourcingRepository.GetById (TODO → пустой список) и реконструкция всегда DefaultAnemicModel.
builder.Services.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IJournal, IJournalAnemicModel>, Journal.Storage.JournalEventSourcingRepository>();
builder.Services.AddEventBusInMemory();
builder.Services.AddReadStorePostgres<IJournal, JournalReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
builder.Services.AddReadStorePostgres<IJournal, PartnersDirectoryReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
builder.Services.AddReadStorePostgres<IJournal, JournalDefaultsReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
builder.Services.AddHostedService<Journal.Api.Events.JournalEventConsumer>();
builder.Services.AddSingleton<Journal.Api.Events.JournalEventHandler>(sp => new Journal.Api.Events.JournalEventHandler(sp.GetRequiredService<IServiceScopeFactory>()));
builder.Services.AddSingleton<Journal.Api.Events.JournalEventStoreHandler>();
builder.Services.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());builder.Services.AddScoped<Journal.Storage.RecordEventReplay>();
// BE-149 (CommandRepository.Postgres 0.3.0): доменный rehydrator Full ES — без него DI падает явно.
builder.Services.AddScoped<DigiTFactory.Libraries.CommandRepository.Postgres.Repositories.IEventStreamRehydrator<IJournal, IJournalAnemicModel>, Journal.Storage.RecordEventRehydrator>();
// BE-151: rebuild read-моделей из Event Store (IRebuildService).
builder.Services.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.Repository.IRebuildService<IJournal>, Journal.Api.Events.JournalRebuildService>();
builder.Services.AddScoped<Journal.Storage.RecordAggregateProvider>();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Journal.Application.IActorProvider, Journal.Api.HttpActorProvider>();
builder.Services.AddSingleton<IJournalEventPublisher, HttpJournalEventPublisher>();

builder.Services.AddHealthChecks().AddCheck<DbHealthCheck>("postgres");
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var es = scope.ServiceProvider.GetRequiredService<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>();
    var migAsm = ((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>)es).Instance.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly>()?.Assembly?.GetName()?.Name;
    Console.WriteLine($"[migrate] asm={typeof(DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext).Assembly.GetName().Name}, migAsm={migAsm}, migrations={string.Join(",", es.Database.GetMigrations())}");
    es.Database.Migrate();
    var readDb = scope.ServiceProvider.GetRequiredService<ReadDbContext>();
    readDb.Database.Migrate();
}
// BE-129: глобальный ловец исключений до контроллера — [journal] EX в WithOwner не ловит,
// т.к. 500 приходит из middleware/routing/DI-конструктора, а не из action.
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex) { Console.WriteLine("[mw] EX: " + ex); throw; }
});
app.UseXdiarySwagger();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();

public partial class Program { }
