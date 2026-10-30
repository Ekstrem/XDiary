namespace Journal.Api.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Journal.Storage;

public sealed class DbHealthCheck : IHealthCheck
{
    private readonly CommandDbContext _db;
    public DbHealthCheck(CommandDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL: доступна")
                : HealthCheckResult.Unhealthy("PostgreSQL: недоступна");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL: недоступна", ex);
        }
    }
}