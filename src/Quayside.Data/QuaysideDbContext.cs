using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Quayside.Domain.Entities;

namespace Quayside.Data;

public class QuaysideDbContext : DbContext
{
    private readonly Func<string?> _currentUser;
    private readonly Func<DateTime> _utcNow;

    public QuaysideDbContext(DbContextOptions<QuaysideDbContext> options,
        Func<string?>? currentUser = null, Func<DateTime>? utcNow = null) : base(options)
    {
        _currentUser = currentUser ?? (() => Environment.UserName);
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public DbSet<Party> Parties => Set<Party>();
    public DbSet<ShippingLine> Lines => Set<ShippingLine>();
    public DbSet<Vessel> Vessels => Set<Vessel>();
    public DbSet<Port> Ports => Set<Port>();
    public DbSet<Depot> Depots => Set<Depot>();
    public DbSet<BodyState> BodyStates => Set<BodyState>();
    public DbSet<Voyage> Voyages => Set<Voyage>();
    public DbSet<Container> Containers => Set<Container>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BillOfLading> BillsOfLading => Set<BillOfLading>();
    public DbSet<ContainerCycle> ContainerCycles => Set<ContainerCycle>();
    public DbSet<DemurrageTariff> DemurrageTariffs => Set<DemurrageTariff>();
    public DbSet<DemurrageSlab> DemurrageSlabs => Set<DemurrageSlab>();
    public DbSet<DemurrageRate> DemurrageRates => Set<DemurrageRate>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();

    private bool IsPostgres => Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;

    /// <summary>SQL filter for "live rows only" unique indexes, per provider.</summary>
    private string LiveFilter => IsPostgres ? "\"IsDeleted\" = false" : "\"IsDeleted\" = 0";

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Soft-delete query filter on every audited entity (BR-G1).
        foreach (var et in b.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)).ToList())
        {
            var p = Expression.Parameter(et.ClrType, "e");
            var body = Expression.Not(Expression.Property(p, nameof(Entity.IsDeleted)));
            b.Entity(et.ClrType).HasQueryFilter(Expression.Lambda(body, p));
        }

        b.Entity<Party>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.NameEn).HasMaxLength(256);
            e.Property(x => x.NameFa).HasMaxLength(256);
            e.HasIndex(x => x.Code).IsUnique().HasFilter(LiveFilter);
        });
        b.Entity<ShippingLine>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(16);
            e.HasIndex(x => x.Code).IsUnique().HasFilter(LiveFilter);
        });
        b.Entity<Vessel>(e => e.HasIndex(x => x.Code).IsUnique().HasFilter(LiveFilter));
        b.Entity<Port>(e => e.HasIndex(x => x.Code).IsUnique().HasFilter(LiveFilter));
        b.Entity<Depot>(e => e.HasIndex(x => x.Name).IsUnique().HasFilter(LiveFilter));
        b.Entity<BodyState>(e => e.HasIndex(x => x.Code).IsUnique().HasFilter(LiveFilter));
        b.Entity<Container>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(11);
            e.Property(x => x.TypeCode).HasMaxLength(4);
            e.HasIndex(x => x.Number).IsUnique().HasFilter(LiveFilter);
        });
        b.Entity<Booking>(e => e.HasIndex(x => x.BookingNo).IsUnique().HasFilter(LiveFilter));
        b.Entity<BillOfLading>(e =>
        {
            e.HasIndex(x => x.HouseNo);
            e.HasMany(x => x.Cycles).WithOne(x => x.BillOfLading!).HasForeignKey(x => x.BillOfLadingId);
        });
        b.Entity<ContainerCycle>(e =>
        {
            e.HasIndex(x => x.ContainerId);
            e.Property(x => x.GrossWeightKg).HasPrecision(18, 3);
            e.Property(x => x.NetWeightKg).HasPrecision(18, 3);
            e.Property(x => x.ReeferTempC).HasPrecision(6, 2);
        });
        b.Entity<DemurrageTariff>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique().HasFilter(LiveFilter);
            e.HasMany(x => x.Slabs).WithOne().HasForeignKey(x => x.TariffId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<DemurrageSlab>(e =>
            e.HasMany(x => x.Rates).WithOne().HasForeignKey(x => x.SlabId).OnDelete(DeleteBehavior.Cascade));
        b.Entity<DemurrageRate>(e =>
        {
            e.Property(x => x.DailyRate).HasPrecision(18, 4);
            e.HasIndex(x => new { x.SlabId, x.EquipmentClass }).IsUnique();
        });
        b.Entity<Invoice>(e =>
        {
            e.HasIndex(x => new { x.TypeCode, x.Serial }).IsUnique().HasFilter(
                "\"Serial\" IS NOT NULL");
            e.HasIndex(x => x.DraftNumber).IsUnique();
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<InvoiceLine>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(18, 4);
            e.Property(x => x.UnitPrice).HasPrecision(18, 4);
            e.Property(x => x.Discount).HasPrecision(18, 4);
            e.Property(x => x.VatPercent).HasPrecision(7, 4);
        });
        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 4);
            e.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PaymentAllocation>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 4);
            e.HasQueryFilter(x => !x.Invoice!.IsDeleted);
        });
        b.Entity<ExchangeRate>(e =>
        {
            e.Property(x => x.Rate).HasPrecision(18, 6);
            e.HasIndex(x => new { x.CurrencyCode, x.EffectiveOn }).IsUnique();
        });
        b.Entity<AppSetting>(e => e.HasIndex(x => x.Key).IsUnique());
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        Stamp();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        Stamp();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    /// <summary>Audit stamping (BR-G3) and conversion of deletes into soft deletes (BR-G1).</summary>
    private void Stamp()
    {
        var now = _utcNow();
        var user = _currentUser();
        foreach (var e in ChangeTracker.Entries<Entity>())
        {
            switch (e.State)
            {
                case EntityState.Added:
                    e.Entity.CreatedAt = now;
                    e.Entity.CreatedBy = user;
                    break;
                case EntityState.Modified:
                    e.Entity.UpdatedAt = now;
                    e.Entity.UpdatedBy = user;
                    break;
                case EntityState.Deleted:
                    e.State = EntityState.Modified;
                    e.Entity.IsDeleted = true;
                    e.Entity.DeletedAt = now;
                    e.Entity.DeletedBy = user;
                    break;
            }
        }
    }
}
