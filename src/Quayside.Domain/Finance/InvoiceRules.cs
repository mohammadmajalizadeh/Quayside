using Quayside.Domain.Entities;
using Quayside.Domain.Rules;

namespace Quayside.Domain.Finance;

/// <summary>Rounding per currency (SPEC OQ-20). Unlisted currencies use <see cref="DefaultDecimals"/>.</summary>
public sealed record CurrencyRounding(IReadOnlyDictionary<string, int> Decimals, int DefaultDecimals = 2)
{
    public static CurrencyRounding Parse(string? text, int defaultDecimals = 2)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = pair.Split('=', StringSplitOptions.TrimEntries);
            if (p.Length != 2 || !int.TryParse(p[1], out var d) || d is < 0 or > 6)
                throw new FormatException($"Invalid currency rounding '{pair}'. Use e.g. IRR=0.");
            map[p[0]] = d;
        }
        return new CurrencyRounding(map, defaultDecimals);
    }

    public decimal Round(decimal amount, string currency) =>
        Math.Round(amount, Decimals.TryGetValue(currency, out var d) ? d : DefaultDecimals, MidpointRounding.AwayFromZero);
}

public sealed record InvoiceTotals(decimal Net, decimal Vat)
{
    public decimal Gross => Net + Vat;
}

public static class InvoiceRules
{
    /// <summary>BR-I4: net = qty × unit price − discount.</summary>
    public static decimal LineNet(InvoiceLine l, string currency, CurrencyRounding r) =>
        r.Round(l.Quantity * l.UnitPrice - l.Discount, currency);

    /// <summary>BR-I4: VAT on the rounded net amount.</summary>
    public static decimal LineVat(InvoiceLine l, string currency, CurrencyRounding r) =>
        r.Round(LineNet(l, currency, r) * l.VatPercent / 100m, currency);

    public static InvoiceTotals Totals(Invoice inv, CurrencyRounding r) => new(
        inv.Lines.Sum(l => LineNet(l, inv.CurrencyCode, r)),
        inv.Lines.Sum(l => LineVat(l, inv.CurrencyCode, r)));

    /// <summary>Effect on the customer's balance: credit notes reduce it.</summary>
    public static decimal SignedGross(Invoice inv, CurrencyRounding r) =>
        inv.Kind == InvoiceKind.CreditNote ? -Totals(inv, r).Gross : Totals(inv, r).Gross;

    /// <summary>BR-I1: drafts are numbered -1, -2, ...</summary>
    public static int NextDraftNumber(int? currentMinimum) => Math.Min(-1, (currentMinimum ?? 0) - 1);

    /// <summary>BR-I1: finals get max(serial of the type) + 1.</summary>
    public static int NextSerial(int? currentMaximum) => (currentMaximum ?? 0) + 1;

    /// <summary>BR-I3: only drafts can be edited.</summary>
    public static void EnsureEditable(Invoice inv)
    {
        if (inv.Status != InvoiceStatus.Draft)
            throw new BusinessRuleException("BR-I3",
                $"Invoice {Describe(inv)} is {inv.Status.ToString().ToLowerInvariant()}; use a credit or debit note to adjust it.");
    }

    public static void Finalize(Invoice inv, int serial)
    {
        EnsureEditable(inv);
        if (inv.Lines.Count == 0) throw new BusinessRuleException("BR-I1", "An invoice needs at least one line before it is finalised.");
        if (inv.PartyId is null && inv.Party is null) throw new BusinessRuleException("BR-I1", "Select the invoice party before finalising.");
        inv.Serial = serial;
        inv.Status = InvoiceStatus.Final;
    }

    /// <summary>Creates a draft credit/debit note adjusting a final invoice.</summary>
    public static Invoice CreateAdjustment(Invoice original, InvoiceKind kind, int draftNumber, DateOnly on,
        IEnumerable<InvoiceLine> lines)
    {
        if (kind is not (InvoiceKind.CreditNote or InvoiceKind.DebitNote))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (original.Status != InvoiceStatus.Final)
            throw new BusinessRuleException("BR-I3", "Only final invoices can be adjusted by credit or debit note.");
        return new Invoice
        {
            Kind = kind, TypeCode = kind == InvoiceKind.CreditNote ? "CN" : "DN", Side = original.Side,
            DraftNumber = draftNumber, IssuedOn = on, PartyId = original.PartyId, BillOfLadingId = original.BillOfLadingId,
            CurrencyCode = original.CurrencyCode, AdjustsInvoiceId = original.Id, Lines = lines.ToList(),
        };
    }

    /// <summary>BR-I2: a service may appear once per container size and type.</summary>
    public static IReadOnlyList<RuleViolation> ValidateNoDuplicateServices(
        IEnumerable<(string ServiceCode, int? SizeFeet, string? TypeCode)> lines) =>
        lines.GroupBy(l => (l.ServiceCode.ToUpperInvariant(), l.SizeFeet, (l.TypeCode ?? "").ToUpperInvariant()))
            .Where(g => g.Count() > 1)
            .Select(g => new RuleViolation("BR-I2",
                $"Service {g.Key.Item1} appears {g.Count()} times for {g.Key.SizeFeet}' {g.Key.Item3}."))
            .ToList();

    /// <summary>BR-I6: exchange rates are frozen on a payment-locked voyage.</summary>
    public static void EnsureRateEditable(Voyage? voyage)
    {
        if (voyage?.PaymentLocked == true)
            throw new BusinessRuleException("BR-I6", "The voyage is locked for payment; its exchange rate cannot change.");
    }

    public static string Describe(Invoice inv) =>
        inv.Serial is { } s ? $"{inv.TypeCode}-{s}" : $"draft {inv.DraftNumber}";
}

public static class PaymentRules
{
    /// <summary>BR-I5: allocations are positive, same currency, within payment and invoice balances.</summary>
    public static void Allocate(Payment payment, Invoice invoice, decimal amount, decimal invoiceOpenBalance)
    {
        var v = new List<RuleViolation>();
        if (amount <= 0) v.Add(new("BR-I5", "Allocation amount must be positive."));
        if (invoice.Status != InvoiceStatus.Final) v.Add(new("BR-I5", "Payments can only be allocated to final invoices."));
        if (!string.Equals(payment.CurrencyCode, invoice.CurrencyCode, StringComparison.OrdinalIgnoreCase))
            v.Add(new("BR-I5", $"Payment currency {payment.CurrencyCode} differs from invoice currency {invoice.CurrencyCode}."));
        var unallocated = payment.Amount - payment.Allocations.Sum(a => a.Amount);
        if (amount > unallocated) v.Add(new("BR-I5", $"Only {unallocated} of the payment is unallocated."));
        if (amount > invoiceOpenBalance) v.Add(new("BR-I5", $"Invoice {InvoiceRules.Describe(invoice)} has only {invoiceOpenBalance} open."));
        if (v.Count > 0) throw new BusinessRuleException(v);
        payment.Allocations.Add(new PaymentAllocation { InvoiceId = invoice.Id, Invoice = invoice, Amount = amount });
    }

    public static void EnsureCanDelete(Payment payment)
    {
        if (payment.Allocations.Count > 0)
            throw new BusinessRuleException("BR-I5", "This payment is allocated to invoice(s) and cannot be deleted.");
    }
}
