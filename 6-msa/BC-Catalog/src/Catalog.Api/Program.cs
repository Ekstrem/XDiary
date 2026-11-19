using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Catalog.Api;
using Catalog.Api.HealthChecks;
using Catalog.Domain;
using Catalog.Domain.Abstraction;
using Catalog.DomainServices;
using Catalog.InternalContracts;
using Catalog.Storage;
using XDiary.Contracts;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using Catalog.Application;

var builder = WebApplication.CreateBuilder(args);
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters { ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "xdiary", ValidateIssuer = true, ValidateAudience = false, ValidateLifetime = true, IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(XDiary.Contracts.JwtSigningKey.Resolve(builder.Configuration["Jwt:SigningKey"], builder.Environment.IsDevelopment())) });
builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(c => c.RegisterModule<RegisterDependencies>());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Catalog.Application.Commands.AddCatalogItemCommand>());
builder.Services.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb"), x => x.MigrationsAssembly("Catalog.Storage")));
builder.Services.AddEventStorePostgres<ICatalog, ICatalogAnemicModel>(builder.Configuration.GetConnectionString("CommandDb") ?? "", null);
builder.Services.AddScoped<IEventStoreRepository<ICatalog, ICatalogAnemicModel>, CatalogEventSourcingRepository>();
builder.Services.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<ICatalog, ICatalogAnemicModel>, CatalogEventSourcingRepository>();
builder.Services.AddScoped<CatalogEventReplay>();
builder.Services.AddEventBusInMemory();
builder.Services.AddReadStorePostgres<ICatalog, CatalogReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
// BE-133: DbContextOptions ПОСЛЕ store-регистраций (порядок как в Journal) — иначе Autofac падает (как client).
builder.Services.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IActorProvider, HttpActorProvider>();
// BE-138: без консьюмера/хендлера catalog_read остаётся пустой (POST /catalog 200, GET /catalog []).
builder.Services.AddHostedService<Catalog.Api.Events.CatalogEventConsumer>();
builder.Services.AddSingleton<Catalog.Api.Events.CatalogEventHandler>();
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
