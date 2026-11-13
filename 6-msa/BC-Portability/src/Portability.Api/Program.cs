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
builder.Services.AddDbContext<CommandDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb"), x => x.MigrationsAssembly("Portability.Storage")));
builder.Services.AddDbContext<ReadDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("ReadDb")));
builder.Services.AddScoped<IPortabilityRepository, PortabilityCommandRepository>();
builder.Services.AddScoped<IPortabilityQueryRepository, PortabilityReadRepository>();
builder.Services.AddHealthChecks().AddCheck<DbHealthCheck>("postgres");
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CommandDbContext>().Database.Migrate();
}
app.UseXdiarySwagger();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.Run();