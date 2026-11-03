using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using DailyMetrics.Api.Events;
using DailyMetrics.Api.HealthChecks;
using DailyMetrics.Domain;
using DailyMetrics.Domain.Abstraction;
using DailyMetrics.DomainServices;
using DailyMetrics.Application;
using DailyMetrics.InternalContracts;
using DailyMetrics.Storage;
using XDiary.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(c => c.RegisterModule<RegisterDependencies>());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<DailyMetrics.Application.Commands.RecordStressCommand>());
builder.Services.AddDbContext<CommandDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("CommandDb")));
builder.Services.AddDbContext<ReadDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("ReadDb")));
builder.Services.AddScoped<IRepository<IDailyMetrics, IDailyMetricsAnemicModel>, DailyMetricsCommandRepository>();
builder.Services.AddScoped<DailyMetricsCommandRepository>();
builder.Services.AddScoped<IDailyMetricsQueryRepository, DailyMetricsReadRepository>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IDailyMetricsEventPublisher, HttpDailyMetricsEventPublisher>();
builder.Services.AddHealthChecks().AddCheck<DbHealthCheck>("postgres");
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<CommandDbContext>().Database.Migrate();
}
app.UseXdiarySwagger();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();