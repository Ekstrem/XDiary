namespace Journal.Api;
using Journal.Storage;

/// <summary>
/// BE-061 (G1): точка запуска одноразовой миграции EF → события.
/// Включается флагом конфигурации DataMigration:Enabled=true (в штатном
/// проде — выключено; запуск — отдельным шагом перед переходом на EDM).
/// </summary>
public sealed class JournalDataMigrationRunner : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IConfiguration _cfg;
    private readonly ILogger<JournalDataMigrationRunner> _log;

    public JournalDataMigrationRunner(IServiceProvider sp, IConfiguration cfg, ILogger<JournalDataMigrationRunner> log)
    {
        _sp = sp;
        _cfg = cfg;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cfg.GetValue<bool>("DataMigration:Enabled"))
        {
            _log.LogInformation("JournalDataMigration: выключено (DataMigration:Enabled=false)");
            return;
        }

        using var scope = _sp.CreateScope();
        var migration = scope.ServiceProvider.GetRequiredService<JournalDataMigration>();
        var migrated = await migration.MigrateAsync(stoppingToken);
        _log.LogInformation("JournalDataMigration: готово, создано событий {Count}", migrated);
    }
}
