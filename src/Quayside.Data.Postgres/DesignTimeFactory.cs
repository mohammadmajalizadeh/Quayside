using Microsoft.EntityFrameworkCore.Design;

namespace Quayside.Data.Postgres;

/// <summary>Used only by <c>dotnet ef migrations add</c>; no database connection is opened.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<QuaysideDbContext>
{
    public QuaysideDbContext CreateDbContext(string[] args) =>
        new DatabaseOptions(DatabaseProvider.Postgres, "Host=localhost;Database=quayside_design;Username=quayside").CreateContext();
}
