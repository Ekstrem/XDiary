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
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("CommandDb"))) throw new InvalidOperationException("ConnectionStrings:CommandDb не задан");
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("ReadDb"))) throw new InvalidOperationException("ConnectionStrings:ReadDb не задан");
// BE-075 (порядок важен, НЕ менять): первая регистрация DbContextOptions выигрывает (TryAdd),
// поэтому AddDbContext с MigrationsAssembly обязан идти ДО AddEventStorePostgres — иначе
// DF-пакет перекроет MigrationsAssembly и Migrate() увидит пустой набор (42P01). Доказано на dev: de5507f0.
builder.Services.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb"), x => x.MigrationsAssembly("Journal.Storage")));
builder.Services.AddEventStorePostgres<IJournal, IJournalAnemicModel>(builder.Configuration.GetConnectionString("CommandDb") ?? "", null);
builder.Services.AddEventBusInMemory();
builder.Services.AddReadStorePostgres<IJournal, JournalReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
builder.Services.AddReadStorePostgres<IJournal, PartnersDirectoryReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
builder.Services.AddReadStorePostgres<IJournal, JournalDefaultsReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
builder.Services.AddHostedService<Journal.Api.Events.JournalEventConsumer>();
builder.Services.AddSingleton<Journal.Api.Events.JournalEventHandler>();
builder.Services.AddScoped<Journal.Storage.JournalDataMigration>();
builder.Services.AddHostedService<Journal.Api.JournalDataMigrationRunner>();
builder.Services.AddDbContext<CommandDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb")));
builder.Services.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
builder.Services.AddScoped<IRepository<IJournal, IJournalAnemicModel>, JournalCommandRepository>();
builder.Services.AddScoped<IJournalQueryRepository, JournalReadRepository>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IJournalEventPublisher, HttpJournalEventPublisher>();

builder.Services.AddHealthChecks().AddCheck<DbHealthCheck>("postgres");
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var es = scope.ServiceProvider.GetRequiredService<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>();
    var migAsm = ((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>)es).Instance.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly>()?.Assembly?.GetName()?.Name;
    Console.WriteLine($"[migrate] asm={typeof(DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext).Assembly.GetName().Name}, migAsm={migAsm}, migrations={string.Join(",", es.Database.GetMigrations())}");
    es.Database.Migrate();
    scope.ServiceProvider.GetRequiredService<CommandDbContext>().Database.Migrate();
    var readDb = scope.ServiceProvider.GetRequiredService<ReadDbContext>();
    readDb.Database.Migrate();
}
app.UseXdiarySwagger();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();

public partial class Program { }
