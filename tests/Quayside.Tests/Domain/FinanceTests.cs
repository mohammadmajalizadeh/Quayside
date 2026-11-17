using Quayside.Data;
using Quayside.Domain.Entities;
using Quayside.Domain.Finance;
using Quayside.Domain.Rules;
using Quayside.Domain.Settings;

namespace Quayside.Tests.Domain;

public class InvoiceRulesTests
{
    private static readonly CurrencyRounding R = CurrencyRounding.Parse("IRR=0");

    [Fact]
    public void BR_I4_line_net_vat_and_rounding_per_currency()
    {
        var l = new InvoiceLine { Quantity = 3, UnitPrice = 10.555m, Discount = 1, VatPercent = 10 };
        Assert.Equal(30.67m, InvoiceRules.LineNet(l, "USD", R));   // 31.665 - 1 = 30.665 -> 30.67
        Assert.Equal(3.07m, InvoiceRules.LineVat(l, "USD", R));    // 3.067 -> 3.07
        var irr = new InvoiceLine { Quantity = 1, UnitPrice = 1_000_000.6m, VatPercent = 9 };
        Assert.Equal(1_000_001m, InvoiceRules.LineNet(irr, "IRR", R));
        Assert.Equal(90_000m, InvoiceRules.LineVat(irr, "IRR", R));
    }

    [Fact]
    public void BR_I4_invoice_totals_and_credit_note_sign()
    {
        var inv = new Invoice
        {
            CurrencyCode = "USD",
            Lines = { new() { Quantity = 2, UnitPrice = 50, VatPercent = 10 }, new() { Quantity = 1, UnitPrice = 20 } },
        };
        var t = InvoiceRules.Totals(inv, R);
        Assert.Equal((120m, 10m, 130m), (t.Net, t.Vat, t.Gross));
        inv.Kind = InvoiceKind.CreditNote;
        Assert.Equal(-130m, InvoiceRules.SignedGross(inv, R));
    }

    [Fact]
    public void BR_I1_draft_numbers_negative_and_serials_increment()
    {
        Assert.Equal(-1, InvoiceRules.NextDraftNumber(null));
        Assert.Equal(-1, InvoiceRules.NextDraftNumber(5));
        Assert.Equal(-8, InvoiceRules.NextDraftNumber(-7));
        Assert.Equal(1, InvoiceRules.NextSerial(null));
        Assert.Equal(43, InvoiceRules.NextSerial(42));
    }

    [Fact]
    public void BR_I1_finalize_requires_lines_and_party()
    {
        var inv = new Invoice();
        Assert.Throws<BusinessRuleException>(() => InvoiceRules.Finalize(inv, 1));
        inv.Lines.Add(new InvoiceLine { Quantity = 1, UnitPrice = 1 });
        Assert.Throws<BusinessRuleException>(() => InvoiceRules.Finalize(inv, 1));
        inv.PartyId = 1;
        InvoiceRules.Finalize(inv, 1);
        Assert.Equal((InvoiceStatus.Final, 1), (inv.Status, inv.Serial!.Value));
    }

    [Fact]
    public void BR_I3_final_invoice_not_editable_adjust_with_credit_note()
    {
        var inv = new Invoice { Id = 9, PartyId = 1, Lines = { new() { Quantity = 1, UnitPrice = 10 } } };
        InvoiceRules.Finalize(inv, 1);
        Assert.Equal("BR-I3", Assert.Throws<BusinessRuleException>(() => InvoiceRules.EnsureEditable(inv)).Violations[0].Rule);

        var cn = InvoiceRules.CreateAdjustment(inv, InvoiceKind.CreditNote, -2, new DateOnly(2026, 4, 1),
            new[] { new InvoiceLine { Quantity = 1, UnitPrice = 4 } });
        Assert.Equal((9, "CN", InvoiceStatus.Draft), (cn.AdjustsInvoiceId!.Value, cn.TypeCode, cn.Status));
        Assert.Throws<BusinessRuleException>(() =>
            InvoiceRules.CreateAdjustment(cn, InvoiceKind.DebitNote, -3, default, Array.Empty<InvoiceLine>()));
    }

    [Fact]
    public void BR_I2_duplicate_service_on_same_size_and_type_rejected()
    {
        Assert.Single(InvoiceRules.ValidateNoDuplicateServices(new (string, int?, string?)[]
        {
            ("THC", 20, "DC"), ("thc", 20, "dc"), ("THC", 40, "DC"),
        }));
        Assert.Empty(InvoiceRules.ValidateNoDuplicateServices(new (string, int?, string?)[] { ("THC", 20, "DC"), ("THC", 40, "DC") }));
    }

    [Fact]
    public void BR_I6_rate_locked_on_payment_locked_voyage()
    {
        InvoiceRules.EnsureRateEditable(new Voyage());
        Assert.Throws<BusinessRuleException>(() => InvoiceRules.EnsureRateEditable(new Voyage { PaymentLocked = true }));
    }

    [Fact]
    public void BR_I5_allocation_limits()
    {
        var inv = new Invoice { Id = 1, Status = InvoiceStatus.Final, CurrencyCode = "USD" };
        var pay = new Payment { Amount = 100, CurrencyCode = "USD" };
        PaymentRules.Allocate(pay, inv, 60, invoiceOpenBalance: 80);
        Assert.Throws<BusinessRuleException>(() => PaymentRules.Allocate(pay, inv, 50, 20));   // > unallocated and > open
        Assert.Throws<BusinessRuleException>(() => PaymentRules.Allocate(pay, inv, 0, 20));
        Assert.Throws<BusinessRuleException>(() =>
            PaymentRules.Allocate(new Payment { Amount = 10, CurrencyCode = "EUR" }, inv, 5, 20));
        Assert.Throws<BusinessRuleException>(() => PaymentRules.EnsureCanDelete(pay));
        PaymentRules.EnsureCanDelete(new Payment());
    }

    [Fact]
    public void Currency_rounding_text_is_validated() =>
        Assert.Throws<FormatException>(() => CurrencyRounding.Parse("IRR=x"));
}

public class FinanceServiceTests
{
    private static (TestDatabase db, QuaysideDbContext ctx, FinanceService fin, BillOfLading bl, Party party) Setup(DatabaseProvider provider)
    {
        TestDatabase.SkipUnlessAvailable(provider);
        var db = TestDatabase.Create(provider);
        var ctx = db.NewContext();
        var party = new Party { Code = "C1", NameEn = "Consignee One", Roles = PartyRole.Client | PartyRole.Consignee };
        var tariff = new DemurrageTariff
        {
            Code = "IMP-STD", Name = "Import standard", CurrencyCode = "USD", FreeDays = 5,
            Slabs =
            {
                new() { FromDay = 6, ToDay = 10, Rates = { new() { EquipmentClass = EquipmentClass.Dry20, DailyRate = 10 },
                                                          new() { EquipmentClass = EquipmentClass.Dry40, DailyRate = 20 } } },
                new() { FromDay = 11, Rates = { new() { EquipmentClass = EquipmentClass.Dry20, DailyRate = 20 },
                                               new() { EquipmentClass = EquipmentClass.Dry40, DailyRate = 40 } } },
            },
        };
        var bl = new BillOfLading { HouseNo = "IMP-9", Direction = Direction.Import, DemurrageTariff = tariff, DeliveryOrderOn = new DateOnly(2026, 3, 2) };
        bl.Cycles.Add(new ContainerCycle
        {
            Direction = Direction.Import, DischargedOn = new DateOnly(2026, 3, 1), EmptyReturnedOn = new DateOnly(2026, 3, 14),
            Container = new Container { Number = "CSQU3054383", SizeFeet = 20, TypeCode = "DC" },
        });
        bl.Cycles.Add(new ContainerCycle
        {
            Direction = Direction.Import, DischargedOn = new DateOnly(2026, 3, 1), EmptyReturnedOn = new DateOnly(2026, 3, 3),
            Container = new Container { Number = "MSKU9070323", SizeFeet = 40, TypeCode = "DC" },
        });
        ctx.AddRange(party, bl);
        ctx.SaveChanges();
        return (db, ctx, new FinanceService(ctx, new QuaysideSettings()), bl, party);
    }

    [SkippableTheory]
    [MemberData(nameof(Data.MigrationTests.Providers), MemberType = typeof(Data.MigrationTests))]
    public void Demurrage_invoice_from_bl_and_one_per_side(DatabaseProvider provider)
    {
        var (db, ctx, fin, bl, party) = Setup(provider);
        using (db) using (ctx)
        {
            var inv = fin.CreateDemurrageInvoice(bl.Id, InvoiceSide.Sell, party.Id, new DateOnly(2026, 3, 20));
            Assert.Equal(InvoiceStatus.Draft, inv.Status);
            Assert.Equal(-1, inv.DraftNumber);
            // 20': 14 days -> 5x10 + 4x20 = 130; 40': 3 days within free time -> 0
            Assert.Equal(130m, InvoiceRules.Totals(inv, fin.Rounding).Net);
            Assert.Equal(3, inv.Lines.Count);

            Assert.Equal("BR-D5", Assert.Throws<BusinessRuleException>(() =>
                fin.CreateDemurrageInvoice(bl.Id, InvoiceSide.Sell, party.Id, new DateOnly(2026, 3, 20))).Violations[0].Rule);
            fin.CreateDemurrageInvoice(bl.Id, InvoiceSide.Buy, party.Id, new DateOnly(2026, 3, 20)); // other side allowed

            // BR-L4: the invoiced container's return date is now locked
            var cycleId = inv.Lines[0].ContainerCycleId!.Value;
            Assert.True(ctx.IsCoveredByDemurrageInvoice(cycleId));
            fin.Cancel(inv);
            Assert.True(ctx.IsCoveredByDemurrageInvoice(cycleId)); // still locked by the buy-side invoice
            fin.CreateDemurrageInvoice(bl.Id, InvoiceSide.Sell, party.Id, new DateOnly(2026, 3, 20)); // re-invoice after cancel
        }
    }

    [SkippableTheory]
    [MemberData(nameof(Data.MigrationTests.Providers), MemberType = typeof(Data.MigrationTests))]
    public void BR_D4_missing_return_date_is_estimate_only(DatabaseProvider provider)
    {
        var (db, ctx, fin, bl, party) = Setup(provider);
        using (db) using (ctx)
        {
            ctx.ContainerCycles.First().EmptyReturnedOn = null;
            ctx.SaveChanges();
            var est = fin.CalculateDemurrage(bl.Id, new DateOnly(2026, 3, 12));
            Assert.Contains(est, e => e.IsEstimate && e.Result!.Total > 0);
            Assert.Contains(Assert.Throws<BusinessRuleException>(() =>
                    fin.CreateDemurrageInvoice(bl.Id, InvoiceSide.Sell, party.Id, new DateOnly(2026, 3, 12))).Violations,
                v => v.Rule == "BR-D4");
        }
    }

    [SkippableTheory]
    [MemberData(nameof(Data.MigrationTests.Providers), MemberType = typeof(Data.MigrationTests))]
    public void Finalize_assigns_serials_per_type_and_statement_tracks_balance(DatabaseProvider provider)
    {
        var (db, ctx, fin, bl, party) = Setup(provider);
        using (db) using (ctx)
        {
            var a = fin.NewDraft(new Invoice { PartyId = party.Id, IssuedOn = new DateOnly(2026, 3, 1), Lines = { new() { Quantity = 1, UnitPrice = 100 } } });
            var b = fin.NewDraft(new Invoice { PartyId = party.Id, IssuedOn = new DateOnly(2026, 3, 2), Lines = { new() { Quantity = 1, UnitPrice = 50, VatPercent = 10 } } });
            var d = fin.CreateDemurrageInvoice(bl.Id, InvoiceSide.Sell, party.Id, new DateOnly(2026, 3, 20));
            Assert.Equal((-1, -2, -3), (a.DraftNumber, b.DraftNumber, d.DraftNumber));
            fin.Finalize(b);
            fin.Finalize(a);
            fin.Finalize(d);
            Assert.Equal((2, 1, 1), (a.Serial!.Value, b.Serial!.Value, d.Serial!.Value)); // DEM has its own series

            var pay = new Payment { PartyId = party.Id, Amount = 120, CurrencyCode = "USD", PaidOn = new DateOnly(2026, 3, 25), Reference = "TT-1" };
            ctx.Payments.Add(pay);
            ctx.SaveChanges();
            fin.Allocate(pay, a, 100);
            Assert.Equal(0m, fin.OpenBalance(a));
            Assert.Throws<BusinessRuleException>(() => fin.Allocate(pay, b, 55)); // only 20 unallocated
            Assert.Throws<BusinessRuleException>(() => fin.DeletePayment(pay));
            Assert.Throws<BusinessRuleException>(() => fin.Cancel(a));

            var soa = fin.Statement(party.Id, "USD");
            Assert.Equal(new[] { 100m, 155m, 285m, 165m }, soa.Select(s => s.Balance)); // 100, +55, +130 DEM, -120
        }
    }
}
