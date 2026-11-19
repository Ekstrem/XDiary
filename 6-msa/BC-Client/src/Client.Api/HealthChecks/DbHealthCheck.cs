namespace Client.Api.HealthChecks;
using DigiTFactory.Libraries.CommandRepository.Postgres;
using Microsoft.Extensions.Diagnostics.HealthChecks;
public sealed class DbHealthCheck : IHealthCheck
{
    private readonly EventStoreDbContext _db;
    public DbHealthCheck(EventStoreDbContext db) => _db = db;
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try { await _db.Database.CanConnectAsync(ct); return HealthCheckResult.Healthy(); }
        catch (Exception ex) { return HealthCheckResult.Unhealthy(ex.Message); }
    }
}