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

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(c => c.RegisterModule<RegisterDependencies>());
builder.Services.AddControllers();
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Client.Application.Commands.RegisterClientCommand>());
builder.Services.AddDbContext<CommandDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb")));
builder.Services.AddDbContext<ReadDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("ReadDb")));
builder.Services.AddScoped<IRepository<IClients, IClientAnemicModel>, ClientCommandRepository>();
builder.Services.AddScoped<IClientQueryRepository, ClientReadRepository>();
builder.Services.AddSingleton<ClientTokenService>();
var otpChannel = builder.Configuration["Otp:Channel"] ?? "log";
OtpChannelGuard.EnsureValid(otpChannel, Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
switch (otpChannel)
{
    case "sms": builder.Services.AddSingleton<Client.Application.Abstraction.IOtpSender, Client.Application.Security.SmsOtpSender>(); break;
    case "telegram": builder.Services.AddSingleton<Client.Application.Abstraction.IOtpSender, Client.Application.Security.TelegramOtpSender>(); break;
    default: builder.Services.AddSingleton<Client.Application.Abstraction.IOtpSender, Client.Application.Security.LogOtpSender>(); break;
}
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
    scope.ServiceProvider.GetRequiredService<CommandDbContext>().Database.Migrate();
}
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();