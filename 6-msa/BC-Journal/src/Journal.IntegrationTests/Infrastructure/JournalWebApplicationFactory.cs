using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Journal.IntegrationTests.Infrastructure;

/// <summary>Тестовый хост Journal: CommandDb/ReadDb в одноразовой БД, test-auth вместо JWT.</summary>
public sealed class JournalWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.Ordinal);

    public JournalWebApplicationFactory(string databaseName)
    {
        DatabaseName = databaseName;
        ConnectionString = JournalTestDatabase.ConnectionStringFor(databaseName);
        SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        SetEnvironmentVariable("JOURNAL_TESTS", "true");
        SetEnvironmentVariable("ConnectionStrings__CommandDb", ConnectionString);
        SetEnvironmentVariable("ConnectionStrings__ReadDb", ConnectionString);
        SetEnvironmentVariable("Jwt__Issuer", "xdiary");
        SetEnvironmentVariable("Jwt__SigningKey", new string('k', 64));
        SetEnvironmentVariable("Logging__LogLevel__Default", "Critical");
        SetEnvironmentVariable("Logging__EventLog__LogLevel__Default", "None");
    }

    public string DatabaseName { get; }
    public string ConnectionString { get; }

    public async Task ShutdownAsync()
    {
        await DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        foreach (var pair in _previousEnvironment) Environment.SetEnvironmentVariable(pair.Key, pair.Value);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Logging:LogLevel:Default", "None");
        builder.UseSetting("Logging:EventLog:LogLevel:Default", "None");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
    }

    private void SetEnvironmentVariable(string name, string value)
    {
        _previousEnvironment[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }
}
