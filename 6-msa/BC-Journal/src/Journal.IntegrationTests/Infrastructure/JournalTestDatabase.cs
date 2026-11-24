using Npgsql;

namespace Journal.IntegrationTests.Infrastructure;

/// <summary>Одноразовая БД journal_test_&lt;8hex&gt; поверх XDIARY_TEST_POSTGRES; боевая БД не используется.</summary>
internal static class JournalTestDatabase
{
    public const string ConnectionStringEnvironmentVariable = "XDIARY_TEST_POSTGRES";
    private const string Prefix = "journal_test_";

    public static string? AdminConnectionString => Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
    public static bool Available => !string.IsNullOrWhiteSpace(AdminConnectionString);

    public static string NewDatabaseName() => Prefix + Guid.NewGuid().ToString("N")[..8];

    public static string ConnectionStringFor(string database)
        => new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = database }.ConnectionString;

    public static async Task CreateAsync(string database)
    {
        EnsureSafeName(database);
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task DropAsync(string database)
    {
        EnsureSafeName(database);
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static void EnsureSafeName(string database)
    {
        if (!database.StartsWith(Prefix, StringComparison.Ordinal) || !database.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new ArgumentException("Unsafe test database name", nameof(database));
    }
}
