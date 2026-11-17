using Microsoft.EntityFrameworkCore;
using Quayside.Domain.Demurrage;
using Quayside.Domain.Entities;
using Quayside.Domain.Finance;
using Quayside.Domain.Rules;
using Quayside.Domain.Settings;

namespace Quayside.Data;

public sealed record CycleDemurrage(ContainerCycle Cycle, DemurrageResult? Result, bool IsEstimate, string? Problem);

public sealed record StatementLine(DateOnly Date, string Reference, decimal Debit, decimal Credit, decimal Balance);

/// <summary>Invoicing, demurrage invoicing, payments and statements (SPEC W5, W6).</summary>
public sealed class FinanceService(QuaysideDbContext db, QuaysideSettings settings)
{
    public CurrencyRounding Rounding => CurrencyRounding.Parse(settings.CurrencyDecimals);

    public Invoice NewDraft(Invoice inv)
    {
        inv.Status = InvoiceStatus.Draft;
        inv.DraftNumber = InvoiceRules.NextDraftNumber(db.Invoices.IgnoreQueryFilters().Min(i => (int?)i.DraftNumber));
        db.Invoices.Add(inv);
        db.SaveChanges();
        return inv;
    }

    public void Finalize(Invoice inv)
    {
        using var tx = db.Database.BeginTransaction();
        var max = db.Invoices.IgnoreQueryFilters().Where(i => i.TypeCode == inv.TypeCode).Max(i => i.Serial);
        InvoiceRules.Finalize(inv, InvoiceRules.NextSerial(max));
        db.SaveChanges();
        tx.Commit();
    }

    public void Cancel(Invoice inv)
    {
        if (inv.Status == InvoiceStatus.Final && db.PaymentAllocations.Any(a => a.InvoiceId == inv.Id))
            throw new BusinessRuleException("BR-I5", "Remove payment allocations before cancelling this invoice.");
        inv.Status = InvoiceStatus.Cancelled;
        db.SaveChanges();
    }

    /// <summary>
    /// Demurrage (import) or detention (export) for each container on the B/L (BR-D2..BR-D4, BR-D6).
    /// Without a return date the result is an estimate to <paramref name="asOf"/>.
    /// </summary>
    public IReadOnlyList<CycleDemurrage> CalculateDemurrage(int billOfLadingId, DateOnly asOf)
    {
        var bl = db.BillsOfLading
            .Include(b => b.DemurrageTariff).ThenInclude(t => t!.Slabs).ThenInclude(s => s.Rates)
            .Include(b => b.Cycles).ThenInclude(c => c.Container)
            .Single(b => b.Id == billOfLadingId);
        if (bl.DemurrageTariff is null)
            throw new BusinessRuleException("BR-D6", $"B/L {bl.HouseNo} has no demurrage tariff.");
        var options = DemurrageOptions.From(settings);

        var results = new List<CycleDemurrage>();
        foreach (var c in bl.Cycles.OrderBy(c => c.Container!.Number))
        {
            var (start, end) = c.Direction == Direction.Import
                ? (c.DischargedOn, c.EmptyReturnedOn)
                : (c.ReleasedToShipperOn, c.FullReturnedOn);
            if (start is null)
            {
                results.Add(new(c, null, false, c.Direction == Direction.Import ? "No discharge date." : "No release date."));
                continue;
            }
            try
            {
                var cls = DemurrageCalculator.Classify(c.Container!.SizeFeet, c.Container.TypeCode, options);
                var result = DemurrageCalculator.Calculate(bl.DemurrageTariff, start.Value, end ?? asOf, cls, options);
                results.Add(new(c, result, end is null, null));
            }
            catch (BusinessRuleException ex)
            {
                results.Add(new(c, null, end is null, ex.Message));
            }
        }
        return results;
    }

    /// <summary>Creates a draft demurrage invoice for the B/L (BR-D4, BR-D5).</summary>
    public Invoice CreateDemurrageInvoice(int billOfLadingId, InvoiceSide side, int partyId, DateOnly issuedOn)
    {
        if (db.Invoices.Any(i => i.BillOfLadingId == billOfLadingId && i.Kind == InvoiceKind.Demurrage
                                 && i.Side == side && i.Status != InvoiceStatus.Cancelled))
            throw new BusinessRuleException("BR-D5", "This B/L already has a demurrage invoice on this side; cancel it to re-invoice.");

        var calc = CalculateDemurrage(billOfLadingId, issuedOn);
        var problems = calc.Where(c => c.Problem is not null)
            .Select(c => new RuleViolation("BR-D3", $"{c.Cycle.Container!.Number}: {c.Problem}"))
            .Concat(calc.Where(c => c.IsEstimate && c.Problem is null)
                .Select(c => new RuleViolation("BR-D4", $"{c.Cycle.Container!.Number} has no return date; only an estimate is possible.")))
            .ToList();
        if (problems.Count > 0) throw new BusinessRuleException(problems);

        var tariff = db.BillsOfLading.Include(b => b.DemurrageTariff).Single(b => b.Id == billOfLadingId).DemurrageTariff!;
        var inv = new Invoice
        {
            Kind = InvoiceKind.Demurrage, TypeCode = "DEM", Side = side, BillOfLadingId = billOfLadingId,
            PartyId = partyId, IssuedOn = issuedOn, CurrencyCode = tariff.CurrencyCode, DemurrageTariffId = tariff.Id,
        };
        foreach (var c in calc)
        {
            var r = c.Result!;
            if (r.Lines.Count == 0)
            {
                // keep a zero line so the container's dates are locked by this invoice (BR-L4)
                inv.Lines.Add(new InvoiceLine
                {
                    Description = $"{c.Cycle.Container!.Number} within free time ({r.TotalDays} days)",
                    ContainerCycleId = c.Cycle.Id, Quantity = 0, PeriodFrom = r.Start, PeriodTo = r.End,
                    FreeDays = r.FreeDays, ChargeableDays = 0,
                });
                continue;
            }
            foreach (var l in r.Lines)
                inv.Lines.Add(new InvoiceLine
                {
                    Description = $"{c.Cycle.Container!.Number} {r.EquipmentClass} days {l.FromDay}-{l.ToDay}",
                    ServiceCode = "DEM", ContainerCycleId = c.Cycle.Id, Quantity = l.Days, UnitPrice = l.DailyRate,
                    PeriodFrom = r.Start, PeriodTo = r.End, FreeDays = r.FreeDays, ChargeableDays = r.ChargeableDays,
                });
        }
        return NewDraft(inv);
    }

    public decimal OpenBalance(Invoice inv) =>
        InvoiceRules.Totals(inv, Rounding).Gross
        // decimal aggregation client-side: SQLite cannot SUM decimals in SQL
        - db.PaymentAllocations.Where(a => a.InvoiceId == inv.Id).Select(a => a.Amount).AsEnumerable().Sum();

    public void Allocate(Payment payment, Invoice invoice, decimal amount)
    {
        db.Entry(invoice).Collection(i => i.Lines).Load();
        PaymentRules.Allocate(payment, invoice, amount, OpenBalance(invoice));
        db.SaveChanges();
    }

    public void DeletePayment(Payment payment)
    {
        db.Entry(payment).Collection(p => p.Allocations).Load();
        PaymentRules.EnsureCanDelete(payment);
        db.Payments.Remove(payment);
        db.SaveChanges();
    }

    /// <summary>Statement of account for one party and currency: final sell invoices/notes vs. payments.</summary>
    public IReadOnlyList<StatementLine> Statement(int partyId, string currency)
    {
        var r = Rounding;
        var entries = db.Invoices.Include(i => i.Lines)
            .Where(i => i.PartyId == partyId && i.Status == InvoiceStatus.Final && i.Side == InvoiceSide.Sell
                        && i.CurrencyCode == currency)
            .AsEnumerable()
            .Select(i => (i.IssuedOn, Ref: InvoiceRules.Describe(i), Amount: InvoiceRules.SignedGross(i, r)))
            .Concat(db.Payments.Where(p => p.PartyId == partyId && p.CurrencyCode == currency).AsEnumerable()
                .Select(p => (IssuedOn: p.PaidOn, Ref: $"Payment {p.Reference ?? p.Id.ToString()}", Amount: -p.Amount)))
            .OrderBy(e => e.IssuedOn).ThenBy(e => e.Ref, StringComparer.Ordinal);

        var balance = 0m;
        var lines = new List<StatementLine>();
        foreach (var e in entries)
        {
            balance += e.Amount;
            lines.Add(new StatementLine(e.IssuedOn, e.Ref, Math.Max(e.Amount, 0), Math.Max(-e.Amount, 0), balance));
        }
        return lines;
    }
}
