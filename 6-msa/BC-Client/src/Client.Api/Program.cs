using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Client.Api.HealthChecks;
using Client.Api.Security;
using Client.Domain;
using Client.Domain.Abstraction;
using Client.DomainServices;
using Client.InternalContracts;
using Client.Storage;
using XDiary.Contracts;
using DigiTFactory.Libraries.CommandRepository.Postgres.Extensions;
using DigiTFactory.Libraries.CommandRepository.Postgres.Repositories;
using DigiTFactory.Libraries.EventBus.InMemory.Extensions;
using DigiTFactory.Libraries.ReadRepository.Postgres.Extensions;
using Client.Application;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(c => c.RegisterModule<RegisterDependencies>());
builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Client.Application.Commands.RegisterClientCommand>());
builder.Services.AddDbContext<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb"), x => x.MigrationsAssembly("Client.Storage")));
builder.Services.AddEventStorePostgres<IClients, IClientAnemicModel>(builder.Configuration.GetConnectionString("CommandDb") ?? "", null);
builder.Services.AddScoped<IEventStoreRepository<IClients, IClientAnemicModel>, ClientEventSourcingRepository>();
builder.Services.AddScoped<DigiTFactory.Libraries.SeedWorks.TacticalPatterns.IAnemicModelRepository<IClients, IClientAnemicModel>, ClientEventSourcingRepository>();
builder.Services.AddScoped<ClientEventReplay>();
builder.Services.AddEventBusInMemory();
builder.Services.AddReadStorePostgres<IClients, ClientReadModel, ReadDbContext>(builder.Configuration.GetConnectionString("ReadDb") ?? "", null);
// BE-133 (@TL 00:50): DbContextOptions ПОСЛЕ store-регистраций (порядок как в Journal, строка 55) —
// иначе не-generic регистрация до AddReadStorePostgres ломает резолв EventStoreDbContext.
builder.Services.AddSingleton<Microsoft.EntityFrameworkCore.DbContextOptions>(sp => sp.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<DigiTFactory.Libraries.CommandRepository.Postgres.EventStoreDbContext>>());
builder.Services.AddSingleton<ClientTokenService>();
var otpChannel = builder.Configuration["Otp:Channel"] ?? "log";
OtpChannelGuard.EnsureValid(otpChannel, Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
switch (otpChannel)
{
    case "sms": builder.Services.AddSingleton<Client.Application.Abstraction.IOtpSender, Client.Application.Security.SmsOtpSender>(); break;
    case "telegram": builder.Services.AddSingleton<Client.Application.Abstraction.IOtpSender, Client.Application.Security.TelegramOtpSender>(); break;
    default: builder.Services.AddSingleton<Client.Application.Abstraction.IOtpSender, Client.Application.Security.LogOtpSender>(); break;
}
// BE-092: лимит переотправки OTP (singleton — in-memory, per-service).
builder.Services.AddSingleton<Client.Application.Abstraction.IOtpRateLimiter, Client.Application.Security.OtpRateLimiter>();
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