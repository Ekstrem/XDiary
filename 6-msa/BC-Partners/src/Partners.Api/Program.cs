using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Partners.Api;
using Partners.Api.HealthChecks;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.DomainServices;
using Partners.InternalContracts;
using Partners.Storage;
using XDiary.Contracts;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using Partners.Application;

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
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Partners.Application.Commands.RegisterPartnerCommand>());
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("CommandDb"))) throw new InvalidOperationException("ConnectionStrings:CommandDb не задан");
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("ReadDb"))) throw new InvalidOperationException("ConnectionStrings:ReadDb не задан");
// BE-075 (порядок важен, НЕ менять): AddDbContext с MigrationsAssembly обязан идти ДО AddEventStorePostgres.
builder.Services.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb"), x => x.MigrationsAssembly("Partners.Storage")));
builder.Services.AddEventStorePostgres<IPartners, IPartnerAnemicModel>(builder.Configuration.GetConnectionString("CommandDb") ?? "", null);
builder.Services.AddScoped<IEventStoreRepository<IPartners, IPartnerAnemicModel>, PartnersEventSourcingRepository>();
builder.Services.AddScoped<PartnersEventReplay>();
builder.Services.AddEventBusInMemory();
// BE-082: связь Partners → Journal (partners_directory).
builder.Services.AddHttpClient();
builder.Services.AddSingleton<Partners.InternalContracts.IPartnerEventPublisher, Partners.Api.Events.PartnerEventPublisher>();
builder.Services.AddHostedService<Partners.Api.Events.PartnerEventConsumer>();
builder.Services.AddSingleton<Partners.Api.Events.PartnerEventHandler>();
builder.Services.AddHostedService<Partners.Api.Events.OutboxDispatcher>();
builder.Services.AddReadStorePostgres<IPartners, PartnersReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
builder.Services.AddDbContext<OutboxDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IActorProvider, HttpActorProvider>();
builder.Services.AddHealthChecks().AddCheck<DbHealthCheck>("postgres");
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var es = scope.ServiceProvider.GetRequiredService<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>();
    var migAsm = ((Microsoft.EntityFrameworkCore.Infrastructure.IInfrastructure<IServiceProvider>)es).Instance.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly>()?.Assembly?.GetName()?.Name;
    Console.WriteLine($"[migrate] asm={typeof(DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext).Assembly.GetName().Name}, migAsm={migAsm}, migrations={string.Join(",", es.Database.GetMigrations())}");
    es.Database.Migrate();
    scope.ServiceProvider.GetRequiredService<OutboxDbContext>().Database.Migrate();
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
