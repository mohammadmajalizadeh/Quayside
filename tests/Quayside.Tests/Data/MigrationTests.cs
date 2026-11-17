using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Quayside.Data;
using Quayside.Domain.Entities;

namespace Quayside.Tests.Data;

public class MigrationTests
{
    public static TheoryData<DatabaseProvider> Providers => new() { DatabaseProvider.Sqlite, DatabaseProvider.Postgres };

    private static void SkipIfUnavailable(DatabaseProvider p) =>
        Skip.If(p == DatabaseProvider.Postgres && !TestDatabase.PostgresAvailable,
            $"PostgreSQL not configured (set {TestDatabase.PgEnv})");

    [SkippableTheory]
    [MemberData(nameof(Providers))]
    public void Migrations_apply_cleanly_on_empty_database(DatabaseProvider provider)
    {
        SkipIfUnavailable(provider);
        using var db = TestDatabase.Create(provider);
        using var ctx = db.NewContext();

        Assert.Empty(ctx.Database.GetPendingMigrations());
        Assert.Equal(ctx.Database.GetMigrations(), ctx.Database.GetAppliedMigrations());
    }

    [SkippableTheory]
    [MemberData(nameof(Providers))]
    public void Migrations_match_current_model(DatabaseProvider provider)
    {
        SkipIfUnavailable(provider);
        using var db = TestDatabase.Create(provider);
        using var ctx = db.NewContext(migrate: false);

        var snapshot = ctx.GetService<IMigrationsAssembly>().ModelSnapshot?.Model;
        Assert.NotNull(snapshot);
        if (snapshot is IMutableModel mutable) snapshot = mutable.FinalizeModel();
        snapshot = ctx.GetService<IModelRuntimeInitializer>().Initialize(snapshot);

        var diffs = ctx.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshot.GetRelationalModel(),
            ctx.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.True(diffs.Count == 0,
            $"Model has changes not captured in {provider} migrations: " +
            string.Join(", ", diffs.Select(d => d.GetType().Name)));
    }

    [SkippableTheory]
    [MemberData(nameof(Providers))]
    public void Soft_deleted_container_frees_its_number_and_is_hidden(DatabaseProvider provider)
    {
        SkipIfUnavailable(provider);
        using var db = TestDatabase.Create(provider);
        using (var ctx = db.NewContext())
        {
            ctx.Containers.Add(new Container { Number = "MSCU1234565", SizeFeet = 20, TypeCode = "DC" });
            ctx.SaveChanges();
            ctx.Containers.Remove(ctx.Containers.Single());
            ctx.SaveChanges();
            ctx.Containers.Add(new Container { Number = "MSCU1234565", SizeFeet = 40, TypeCode = "HC" });
            ctx.SaveChanges();
        }
        using (var ctx = db.NewContext(migrate: false))
        {
            var live = Assert.Single(ctx.Containers);
            Assert.Equal(40, live.SizeFeet);
            var deleted = ctx.Containers.IgnoreQueryFilters().Single(c => c.IsDeleted);
            Assert.Equal("test", deleted.DeletedBy);
            Assert.NotNull(deleted.DeletedAt);
            Assert.Equal("test", live.CreatedBy);
        }
    }

    [SkippableTheory]
    [MemberData(nameof(Providers))]
    public void Duplicate_live_container_number_is_rejected(DatabaseProvider provider)
    {
        SkipIfUnavailable(provider);
        using var db = TestDatabase.Create(provider);
        using var ctx = db.NewContext();
        ctx.Containers.Add(new Container { Number = "MSCU1234565", SizeFeet = 20, TypeCode = "DC" });
        ctx.Containers.Add(new Container { Number = "MSCU1234565", SizeFeet = 20, TypeCode = "DC" });
        Assert.Throws<DbUpdateException>(() => ctx.SaveChanges());
    }
}
