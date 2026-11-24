using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Portability.Api.HealthChecks;
using Portability.Domain;
using Portability.Domain.Abstraction;
using Portability.DomainServices;
using Portability.InternalContracts;
using Portability.Storage;
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
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Portability.Application.Commands.ExportRecordsCommand>());
// Явно указана сборка миграций (контекст в Portability.Storage — по умолчанию EF и так берёт сборку контекста).
builder.Services.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb"), x => x.MigrationsAssembly("Portability.Storage").EnableRetryOnFailure()));
builder.Services.AddEventStorePostgres<IPortability, ICsvDocumentAnemicModel>(builder.Configuration.GetConnectionString("CommandDb") ?? "", null);
builder.Services.AddScoped<IEventStoreRepository<IPortability, ICsvDocumentAnemicModel>, PortabilityEventSourcingRepository>();
builder.Services.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IPortability, ICsvDocumentAnemicModel>, PortabilityEventSourcingRepository>();
builder.Services.AddScoped<PortabilityEventReplay>();
// BE-149 (CommandRepository.Postgres 0.3.0): доменный rehydrator Full ES — без него DI падает явно.
builder.Services.AddScoped<IEventStreamRehydrator<IPortability, ICsvDocumentAnemicModel>, PortabilityEventRehydrator>();
builder.Services.AddEventBusInMemory();
builder.Services.AddReadStorePostgres<IPortability, CsvDocumentReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
// BE-125/BE-133: не-generic DbContextOptions для библиотечного EventStoreDbContext (иначе Autofac падает).
builder.Services.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
// BE-140: без консьюмера/хендлера проекции csv_document_read остаётся пустой (класс BE-134/BE-138).
builder.Services.AddHostedService<Portability.Api.Events.PortabilityEventConsumer>();
builder.Services.AddSingleton<Portability.Api.Events.PortabilityEventHandler>();
builder.Services.AddSingleton<Portability.Api.Events.PortabilityEventStoreHandler>();



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