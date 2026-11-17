using Microsoft.EntityFrameworkCore;
using Quayside.Domain.Entities;

namespace Quayside.Data;

public static class Queries
{
    /// <summary>True when a live, non-cancelled demurrage invoice has a line for this cycle (BR-L4).</summary>
    public static bool IsCoveredByDemurrageInvoice(this QuaysideDbContext db, int cycleId) =>
        cycleId > 0 && db.Invoices.Any(i => i.Kind == InvoiceKind.Demurrage && i.Status != InvoiceStatus.Cancelled
                                            && i.Lines.Any(l => l.ContainerCycleId == cycleId));

    public static int LiveCycleCount(this QuaysideDbContext db, int billOfLadingId) =>
        db.ContainerCycles.Count(c => c.BillOfLadingId == billOfLadingId);

    /// <summary>Tracked entity lookup that also sees rows added (not yet saved) in this context.</summary>
    public static T? FindLocalFirst<T>(this DbSet<T> set, Func<T, bool> local, System.Linq.Expressions.Expression<Func<T, bool>> db)
        where T : Entity =>
        set.Local.FirstOrDefault(x => !x.IsDeleted && local(x)) ?? set.FirstOrDefault(db);
}
