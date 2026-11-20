using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using DailyMetrics.Api;
using DailyMetrics.Api.Events;
using DailyMetrics.Api.HealthChecks;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.DomainServices;
using DailyMetrics.Application;
using DailyMetrics.InternalContracts;
using DailyMetrics.Storage;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using XDiary.Contracts;

var builder = WebApplication.CreateBuilder(args);
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters { ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "xdiary", ValidateIssuer = true, ValidateAudience = false, ValidateLifetime = true, IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(XDiary.Contracts.JwtSigningKey.Resolve(builder.Configuration["Jwt:SigningKey"], builder.Environment.IsDevelopment())) });
builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(c => c.RegisterModule<RegisterDependencies>());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DailyMetrics.Application.Commands.RecordStressCommand>());
builder.Services.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb"), x => x.MigrationsAssembly("DailyMetrics.Storage").EnableRetryOnFailure()));
builder.Services.AddEventStorePostgres<IDailyMetrics, IDailyMetricsAnemicModel>(builder.Configuration.GetConnectionString("CommandDb") ?? "", null);
builder.Services.AddScoped<IEventStoreRepository<IDailyMetrics, IDailyMetricsAnemicModel>, DailyMetricsEventSourcingRepository>();
builder.Services.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IDailyMetrics, IDailyMetricsAnemicModel>, DailyMetricsEventSourcingRepository>();
builder.Services.AddScoped<DailyMetricsEventReplay>();
builder.Services.AddEventBusInMemory();
builder.Services.AddReadStorePostgres<IDailyMetrics, DailyMetricsReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
// BE-133: DbContextOptions ПОСЛЕ store-регистраций (порядок как в Journal) — иначе Autofac падает.
builder.Services.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IActorProvider, HttpActorProvider>();
// BE-140: без консьюмера/хендлера проекции daily_metrics_read остаётся пустой (класс BE-134/BE-138).
builder.Services.AddHostedService<DailyMetrics.Api.Events.DailyMetricsEventConsumer>();
builder.Services.AddSingleton<DailyMetrics.Api.Events.DailyMetricsEventHandler>();
builder.Services.AddSingleton<DailyMetrics.Api.Events.DailyMetricsEventStoreHandler>();




builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDailyMetricsEventPublisher, HttpDailyMetricsEventPublisher>();
builder.Services.AddHealthChecks().AddCheck<DbHealthCheck>("postgres");
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