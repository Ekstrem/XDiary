using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using AppleHealthGateway.Api;
using AppleHealthGateway.Api.HealthChecks;
using AppleHealthGateway.Application;
using AppleHealthGateway.Domain;
using AppleHealthGateway.Domain.Abstraction;
using AppleHealthGateway.DomainServices;
using AppleHealthGateway.InternalContracts;
using AppleHealthGateway.Storage;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using XDiary.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(c => c.RegisterModule<RegisterDependencies>());
builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<AppleHealthGateway.Application.Commands.ConfigureCommand>());
builder.Services.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb"), x => x.MigrationsAssembly("AppleHealthGateway.Storage").EnableRetryOnFailure()));
builder.Services.AddEventStorePostgres<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>(builder.Configuration.GetConnectionString("CommandDb") ?? "", null);
builder.Services.AddScoped<IEventStoreRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>, AppleHealthGatewayEventSourcingRepository>();
builder.Services.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>, AppleHealthGatewayEventSourcingRepository>();
builder.Services.AddScoped<AppleHealthGatewayEventReplay>();
// BE-149 (CommandRepository.Postgres 0.3.0): доменный rehydrator Full ES — без него DI падает явно.
builder.Services.AddScoped<IEventStreamRehydrator<IAppleHealthGateway, IAppleHealthGatewayAnemicModel>, AppleHealthGatewayEventRehydrator>();
builder.Services.AddEventBusInMemory();
builder.Services.AddReadStorePostgres<IAppleHealthGateway, AppleImportReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
// BE-133: DbContextOptions ПОСЛЕ store-регистраций (порядок как в Journal) — иначе Autofac падает.
builder.Services.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IActorProvider, HttpActorProvider>();
// BE-140: без консьюмера/хендлера проекции apple_health_read остаётся пустой (класс BE-134/BE-138).
builder.Services.AddHostedService<AppleHealthGateway.Api.Events.AppleHealthGatewayEventConsumer>();
builder.Services.AddSingleton<AppleHealthGateway.Api.Events.AppleHealthGatewayEventHandler>();
builder.Services.AddSingleton<AppleHealthGateway.Api.Events.AppleHealthGatewayEventStoreHandler>();



builder.Services.AddHealthChecks().AddCheck<DbHealthCheck>("postgres");
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
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<ReadDbContext>().Database.Migrate();
}
app.UseXdiarySwagger();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();