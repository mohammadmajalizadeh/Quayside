using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Quayside.Data;

public enum DatabaseProvider { Sqlite, Postgres }

/// <summary>
/// Database selection. Loaded from <c>quayside.json</c> (user profile) or environment:
/// <c>QUAYSIDE_DB_PROVIDER</c> = Sqlite|Postgres, <c>QUAYSIDE_DB_CONNECTION</c> = connection string.
/// </summary>
public sealed record DatabaseOptions(DatabaseProvider Provider, string ConnectionString)
{
    public const string SqliteMigrationsAssembly = "Quayside.Data.Sqlite";
    public const string PostgresMigrationsAssembly = "Quayside.Data.Postgres";

    public static string DefaultDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Quayside");

    public static DatabaseOptions DefaultSqlite() =>
        new(DatabaseProvider.Sqlite, $"Data Source={Path.Combine(DefaultDataDirectory, "quayside.db")}");

    public static DatabaseOptions Load(string? configPath = null)
    {
        var envProvider = Environment.GetEnvironmentVariable("QUAYSIDE_DB_PROVIDER");
        var envConn = Environment.GetEnvironmentVariable("QUAYSIDE_DB_CONNECTION");
        if (!string.IsNullOrWhiteSpace(envProvider) && !string.IsNullOrWhiteSpace(envConn))
            return new(Enum.Parse<DatabaseProvider>(envProvider, ignoreCase: true), envConn);

        configPath ??= Path.Combine(DefaultDataDirectory, "quayside.json");
        if (File.Exists(configPath))
        {
            var cfg = JsonSerializer.Deserialize<ConfigFile>(File.ReadAllText(configPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (cfg?.Database is { ConnectionString.Length: > 0 } db)
                return new(Enum.Parse<DatabaseProvider>(db.Provider ?? "Sqlite", ignoreCase: true), db.ConnectionString);
        }
        return DefaultSqlite();
    }

    public DbContextOptions<QuaysideDbContext> BuildOptions()
    {
        var b = new DbContextOptionsBuilder<QuaysideDbContext>();
        Configure(b);
        return b.Options;
    }

    public void Configure(DbContextOptionsBuilder b)
    {
        switch (Provider)
        {
            case DatabaseProvider.Sqlite:
                b.UseSqlite(ConnectionString, o => o.MigrationsAssembly(SqliteMigrationsAssembly));
                break;
            case DatabaseProvider.Postgres:
                b.UseNpgsql(ConnectionString, o => o.MigrationsAssembly(PostgresMigrationsAssembly));
                break;
            default:
                throw new NotSupportedException(Provider.ToString());
        }
    }

    public QuaysideDbContext CreateContext(Func<string?>? currentUser = null, Func<DateTime>? utcNow = null)
    {
        if (Provider == DatabaseProvider.Sqlite)
        {
            var path = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(ConnectionString).DataSource;
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir) && path != ":memory:") Directory.CreateDirectory(dir);
        }
        return new QuaysideDbContext(BuildOptions(), currentUser, utcNow);
    }

    private sealed class ConfigFile
    {
        public DbSection? Database { get; set; }
    }

    private sealed class DbSection
    {
        public string? Provider { get; set; }
        public string ConnectionString { get; set; } = "";
    }
}
