using XDiary.Contracts;
using Analytics.Api.Precompute;
using Analytics.Application.Queries;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters { ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "xdiary", ValidateIssuer = true, ValidateAudience = false, ValidateLifetime = true, IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(XDiary.Contracts.JwtSigningKey.Resolve(builder.Configuration["Jwt:SigningKey"], builder.Environment.IsDevelopment())) });
builder.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Analytics Microservice API",
        Version = "v1",
        Description = "BC-Analytics: read-only аналитика (проекции над Журналом и Соматикой). CEQRS, профиль Single."
    });
});

// Read-only: нет агрегата, нет Event Store, нет проекций. Только query-обработчики.
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetSummaryHandler).Assembly));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AnalyticsSnapshot>();
builder.Services.AddHostedService<AnalyticsPrecomputeService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseXdiarySwagger();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

app.Run();
