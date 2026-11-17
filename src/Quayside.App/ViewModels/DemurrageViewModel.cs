using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Quayside.Data;
using Quayside.Domain.Entities;
using Quayside.Domain.Finance;

namespace Quayside.App.ViewModels;

public sealed record DemurrageRow(string Container, string Class, DateOnly? Start, DateOnly? End, int Days, int FreeDays,
    int Chargeable, decimal Amount, string Currency, string Note);

public sealed record InvoiceRow(int Id, string Number, InvoiceKind Kind, InvoiceSide Side, InvoiceStatus Status,
    DateOnly IssuedOn, string? Party, decimal Net, decimal Vat, decimal Gross, string Currency);

public sealed partial class DemurrageViewModel(AppServices services) : ObservableObject
{
    public ObservableCollection<Lookup> Bills { get; } = new();
    public ObservableCollection<Lookup> Parties { get; } = new();
    public ObservableCollection<DemurrageRow> Rows { get; } = new();
    public ObservableCollection<InvoiceRow> Invoices { get; } = new();
    public InvoiceSide[] Sides { get; } = Enum.GetValues<InvoiceSide>();

    [ObservableProperty] private Lookup? _bill;
    [ObservableProperty] private Lookup? _party;
    [ObservableProperty] private InvoiceSide _side = InvoiceSide.Sell;
    [ObservableProperty] private DateTime? _asOf = DateTime.Today;
    [ObservableProperty] private InvoiceRow? _selectedInvoice;
    [ObservableProperty] private string _total = "";

    [RelayCommand]
    public void Refresh()
    {
        using var db = services.NewContext();
        Bills.Clear();
        foreach (var b in db.BillsOfLading.OrderByDescending(b => b.Id).Take(500).Select(b => new Lookup(b.Id, b.HouseNo))) Bills.Add(b);
        Parties.Clear();
        foreach (var p in db.Parties.OrderBy(p => p.NameEn).Select(p => new Lookup(p.Id, p.Code + " - " + p.NameEn))) Parties.Add(p);
        LoadInvoices(db);
    }

    partial void OnBillChanged(Lookup? value)
    {
        Rows.Clear();
        Total = "";
        using var db = services.NewContext();
        LoadInvoices(db);
    }

    [RelayCommand]
    private void Calculate()
    {
        if (Bill is null) return;
        AppServices.Try(() =>
        {
            using var db = services.NewContext();
            var fin = new FinanceService(db, SettingsStore.Load(db));
            var calc = fin.CalculateDemurrage(Bill.Id, AppServices.ToDate(AsOf) ?? DateOnly.FromDateTime(DateTime.Today));
            Rows.Clear();
            foreach (var c in calc)
            {
                var r = c.Result;
                Rows.Add(new DemurrageRow(c.Cycle.Container!.Number, r?.EquipmentClass.ToString() ?? "", r?.Start, r?.End,
                    r?.TotalDays ?? 0, r?.FreeDays ?? 0, r?.ChargeableDays ?? 0, r?.Total ?? 0, r?.CurrencyCode ?? "",
                    c.Problem ?? (c.IsEstimate ? "Estimate: no return date yet" : "")));
            }
            Total = string.Join(", ", Rows.GroupBy(r => r.Currency).Where(g => g.Key != "")
                .Select(g => $"{g.Sum(r => r.Amount):N2} {g.Key}"));
        });
    }

    [RelayCommand]
    private void CreateInvoice()
    {
        if (Bill is null || Party is null) return;
        AppServices.Try(() =>
        {
            using var db = services.NewContext();
            new FinanceService(db, SettingsStore.Load(db)).CreateDemurrageInvoice(Bill.Id, Side, Party.Id,
                AppServices.ToDate(AsOf) ?? DateOnly.FromDateTime(DateTime.Today));
        });
        Refresh();
    }

    [RelayCommand]
    private void FinalizeInvoice() => OnInvoice((fin, inv) => fin.Finalize(inv));

    [RelayCommand]
    private void CancelInvoice() => OnInvoice((fin, inv) => fin.Cancel(inv));

    private void OnInvoice(Action<FinanceService, Invoice> action)
    {
        if (SelectedInvoice is null) return;
        AppServices.Try(() =>
        {
            using var db = services.NewContext();
            var inv = db.Invoices.Include(i => i.Lines).Single(i => i.Id == SelectedInvoice.Id);
            action(new FinanceService(db, SettingsStore.Load(db)), inv);
        });
        Refresh();
    }

    private void LoadInvoices(QuaysideDbContext db)
    {
        Invoices.Clear();
        var rounding = CurrencyRounding.Parse(SettingsStore.Load(db).CurrencyDecimals);
        var q = db.Invoices.Include(i => i.Lines).Include(i => i.Party).AsQueryable();
        if (Bill is not null) q = q.Where(i => i.BillOfLadingId == Bill.Id);
        foreach (var i in q.OrderByDescending(i => i.Id).Take(500).ToList())
        {
            var t = InvoiceRules.Totals(i, rounding);
            Invoices.Add(new InvoiceRow(i.Id, InvoiceRules.Describe(i), i.Kind, i.Side, i.Status, i.IssuedOn, i.Party?.NameEn,
                t.Net, t.Vat, t.Gross, i.CurrencyCode));
        }
    }
}
