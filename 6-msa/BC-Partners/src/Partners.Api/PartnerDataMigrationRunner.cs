namespace Partners.Api;

/// <summary>
/// BE-061 (G1): точка запуска одноразовой миграции EF → события.
/// Включается флагом DataMigration:Enabled=true.
/// </summary>
public sealed class PartnerDataMigrationRunner : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _cfg;
    private readonly ILogger<PartnerDataMigrationRunner> _log;

    public PartnerDataMigrationRunner(IServiceProvider sp, IConfiguration cfg, ILogger<PartnerDataMigrationRunner> log)
    {
        _sp = sp;
        _cfg = cfg;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cfg.GetValue<bool>("DataMigration:Enabled"))
        {
            _log.LogInformation("PartnerDataMigration: выключено (DataMigration:Enabled=false)");
            return;
        }

        using var scope = _sp.CreateScope();
        var migration = scope.ServiceProvider.GetRequiredService<PartnerDataMigration>();
        var migrated = await migration.MigrateAsync(stoppingToken);
        _log.LogInformation("PartnerDataMigration: готово, создано событий {Count}", migrated);
    }
}
