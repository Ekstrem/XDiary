using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace XDiary.Contracts;

/// <summary>Общий swagger под флагом Swagger:Enabled (default false; на проде выключен).</summary>
public static class SwaggerExtensions
{
    public static IServiceCollection AddXdiarySwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        return services;
    }

    public static WebApplication UseXdiarySwagger(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        return app;
    }
}