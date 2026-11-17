namespace Partners.Api.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Partners.Storage;

public sealed class DbHealthCheck : IHealthCheck
{
    private readonly OutboxDbContext _db;
    public DbHealthCheck(OutboxDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL: РґРѕСЃС‚СѓРїРЅР°")
                : HealthCheckResult.Unhealthy("PostgreSQL: РЅРµРґРѕСЃС‚СѓРїРЅР°");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL: РЅРµРґРѕСЃС‚СѓРїРЅР°", ex);
        }
    }
}