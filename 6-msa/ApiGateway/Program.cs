using System.Text;
using ApiGateway.Auth;

var builder = WebApplication.CreateBuilder(args);

// YARP reverse proxy — маршруты и кластеры из конфигурации
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// CORS (ADR-006: PWA → шлюз)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// BFF-агрегация: HttpClient для вызовов микросервисов
builder.Services.AddHttpClient();
builder.Services.AddControllers();

// BE-141: BFF-контроллер должен быть защищён и на уровне ASP.NET (defense-in-depth),
// а не только кастомным middleware ниже: иначе одна ошибка в списке анонимных путей
// открывает контроллер, и он отдаёт 500 с текстом ошибки вместо 401.
var jwtKey = Convert.FromBase64String(
    builder.Configuration["Jwt:SigningKey"]
    ?? Convert.ToBase64String(Encoding.UTF8.GetBytes("xdiary-dev-signing-key-32bytes-min!")));
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "xdiary",
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(jwtKey),
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseCors();

// ======================================================
// JWT-проверка (stateless, ADR-008). Тот же signing key, что у privacy.
// Анонимные запросы (без валидного Bearer) отклоняются, кроме auth-эндпоинтов.
// ======================================================
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    // Swagger — анонимно только при Swagger:Enabled=true (@DO 10:51, check_swagger_perimeter): на проде флаг выключен → 401 штатно.
    if (app.Configuration.GetValue<bool>("Swagger:Enabled")
        && path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    // Анонимны только health + онбординг (создание/верификация клиента) + refresh (сам refresh-токен = credential) + stateless verify-token.
    // PIN (pin/set|has|token) требует JWT — валидацию делает сервис (BE-038).
    var anonymous = GatewayRoutes.IsAnonymous(context.Request.Method, path);

    if (anonymous)
    {
        await next();
        return;
    }

    var authHeader = context.Request.Headers.Authorization.ToString();
    if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        var token = authHeader["Bearer ".Length..].Trim();
        if (JwtVerifier.Verify(token, jwtKey))
        {
            await next();
            return;
        }
    }

    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    context.Response.Headers.WWWAuthenticate = "Bearer";
    await context.Response.WriteAsync("Unauthorized");
});

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();
app.MapControllers();
app.MapGet("/health", () => Results.Ok("ok"));

app.Run();
