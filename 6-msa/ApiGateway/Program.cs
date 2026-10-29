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

var app = builder.Build();

app.UseCors();

// ======================================================
// JWT-проверка (stateless, ADR-008). Тот же signing key, что у privacy.
// Анонимные запросы (без валидного Bearer) отклоняются, кроме auth-эндпоинтов.
// ======================================================
var jwtKey = Convert.FromBase64String(
    builder.Configuration["Jwt:SigningKey"]
    ?? Convert.ToBase64String(Encoding.UTF8.GetBytes("xdiary-dev-signing-key-32bytes-min!")));

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    if (path == "/health"
        || path.StartsWith("/api/v1/privacy/", StringComparison.OrdinalIgnoreCase))
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
    await context.Response.WriteAsync("Unauthorized");
});

app.MapReverseProxy();
app.MapControllers();
app.MapGet("/health", () => Results.Ok("ok"));

app.Run();
