using Analytics.Application.Queries;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Single")
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
