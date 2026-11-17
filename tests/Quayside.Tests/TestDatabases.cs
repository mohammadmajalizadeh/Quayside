using Microsoft.EntityFrameworkCore;
using Npgsql;
using Quayside.Data;

namespace Quayside.Tests;

/// <summary>Creates throw-away databases for tests. PostgreSQL requires QUAYSIDE_TEST_PG.</summary>
public sealed class TestDatabase : IDisposable
{
    public const string PgEnv = "QUAYSIDE_TEST_PG";
    private readonly string? _sqlitePath;
    private readonly string? _pgAdminConn;
    private readonly string? _pgDbName;

    public DatabaseOptions Options { get; }

    private TestDatabase(DatabaseOptions options, string? sqlitePath, string? pgAdmin, string? pgDb)
    {
        Options = options;
        _sqlitePath = sqlitePath;
        _pgAdminConn = pgAdmin;
        _pgDbName = pgDb;
    }

    public static bool PostgresAvailable => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PgEnv));

    /// <summary>Skips PostgreSQL tests when not configured, unless QUAYSIDE_REQUIRE_PG=1 (CI), where it fails instead.</summary>
    public static void SkipUnlessAvailable(DatabaseProvider provider)
    {
        if (provider != DatabaseProvider.Postgres || PostgresAvailable) return;
        if (Environment.GetEnvironmentVariable("QUAYSIDE_REQUIRE_PG") == "1")
            throw new InvalidOperationException($"QUAYSIDE_REQUIRE_PG=1 but {PgEnv} is not set.");
        Skip.If(true, $"PostgreSQL not configured (set {PgEnv})");
    }

    public static TestDatabase Create(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Sqlite => CreateSqlite(),
        DatabaseProvider.Postgres => CreatePostgres(),
        _ => throw new NotSupportedException(),
    };

    public static TestDatabase CreateSqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), $"quayside-test-{Guid.NewGuid():N}.db");
        return new TestDatabase(new DatabaseOptions(DatabaseProvider.Sqlite, $"Data Source={path};Pooling=False"), path, null, null);
    }

    public static TestDatabase CreatePostgres()
    {
        var admin = Environment.GetEnvironmentVariable(PgEnv)
                    ?? throw new InvalidOperationException($"{PgEnv} not set");
        var db = $"quayside_test_{Guid.NewGuid():N}";
        using (var c = new NpgsqlConnection(admin))
        {
            c.Open();
            using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{db}\"", c);
            cmd.ExecuteNonQuery();
        }
        var cs = new NpgsqlConnectionStringBuilder(admin) { Database = db, Pooling = false }.ConnectionString;
        return new TestDatabase(new DatabaseOptions(DatabaseProvider.Postgres, cs), null, admin, db);
    }

    /// <summary>Returns a migrated context.</summary>
    public QuaysideDbContext NewContext(bool migrate = true)
    {
        var ctx = Options.CreateContext(() => "test");
        if (migrate) ctx.Database.Migrate();
        return ctx;
    }

    public void Dispose()
    {
        if (_sqlitePath is not null)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { File.Delete(_sqlitePath); } catch (IOException) { }
        }
        if (_pgAdminConn is not null && _pgDbName is not null)
        {
            using var c = new NpgsqlConnection(_pgAdminConn);
            c.Open();
            using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_pgDbName}\" WITH (FORCE)", c);
            cmd.ExecuteNonQuery();
        }
    }
}
