using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Partners.Api.HealthChecks;
using Partners.Domain;
using Partners.Domain.Abstraction;
using Partners.DomainServices;
using Partners.InternalContracts;
using Partners.Storage;
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
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Partners.Application.Commands.RegisterPartnerCommand>());
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("CommandDb"))) throw new InvalidOperationException("ConnectionStrings:CommandDb не задан");
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("ReadDb"))) throw new InvalidOperationException("ConnectionStrings:ReadDb не задан");
builder.Services.AddEventStorePostgres<IPartners, IPartnerAnemicModel>(builder.Configuration.GetConnectionString("CommandDb") ?? "", null);
builder.Services.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb")));
builder.Services.AddEventBusInMemory();
builder.Services.AddHostedService<Partners.Api.Events.PartnerEventConsumer>();
builder.Services.AddSingleton<Partners.Api.Events.PartnerEventHandler>();
builder.Services.AddScoped<Partners.Api.PartnerDataMigration>();
builder.Services.AddHostedService<Partners.Api.PartnerDataMigrationRunner>();
builder.Services.AddReadStorePostgres<IPartners, PartnersReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
builder.Services.AddDbContext<CommandDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb")));
builder.Services.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
builder.Services.AddScoped<IRepository<IPartners, IPartnerAnemicModel>, PartnerCommandRepository>();
builder.Services.AddScoped<IPartnersQueryRepository, PartnerReadRepository>();
builder.Services.AddHealthChecks().AddCheck<DbHealthCheck>("postgres");
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CommandDbContext>().Database.Migrate();
    var readDb = scope.ServiceProvider.GetRequiredService<ReadDbContext>();
        readDb.Database.ExecuteSqlRaw("CREATE TABLE IF NOT EXISTS partners_read (\"Id\" uuid PRIMARY KEY, \"Name\" text NOT NULL, \"Photo\" text NOT NULL, \"Gender\" text NOT NULL, \"Notes\" text NOT NULL, \"ClientId\" uuid NULL, \"DeletedAt\" timestamptz NULL);");
}
app.UseXdiarySwagger();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();

public partial class Program { }
