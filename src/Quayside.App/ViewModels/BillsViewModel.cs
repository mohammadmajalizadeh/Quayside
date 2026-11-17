using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Quayside.Data;
using Quayside.Domain.Entities;
using Quayside.Domain.Rules;

namespace Quayside.App.ViewModels;

public sealed record BlRow(int Id, string HouseNo, Direction Direction, string? Line, DateOnly? DeliveryOrderOn, string? Tariff, int Containers);

public sealed record CycleRow(int Id, string Container, int SizeFeet, string Type, string CargoType, DateOnly? Released,
    DateOnly? FullReturned, DateOnly? Discharged, DateOnly? EmptyReturned, string? Depot);

public sealed record Lookup(int Id, string Name);

public sealed partial class BillsViewModel(AppServices services) : ObservableObject
{
    public ObservableCollection<BlRow> Items { get; } = new();
    public ObservableCollection<CycleRow> Cycles { get; } = new();
    public ObservableCollection<Lookup> Tariffs { get; } = new();
    public ObservableCollection<Lookup> Depots { get; } = new();
    public Direction[] Directions { get; } = Enum.GetValues<Direction>();

    [ObservableProperty] private BlRow? _selected;
    [ObservableProperty] private CycleRow? _selectedCycle;

    // new / edit B/L
    [ObservableProperty] private string _newHouseNo = "";
    [ObservableProperty] private Direction _newDirection = Direction.Import;
    [ObservableProperty] private Lookup? _tariff;
    [ObservableProperty] private DateTime? _deliveryOrderDate;

    // container event inputs
    [ObservableProperty] private DateTime? _eventDate = DateTime.Today;
    [ObservableProperty] private Lookup? _depot;

    partial void OnSelectedChanged(BlRow? value)
    {
        DeliveryOrderDate = AppServices.ToDateTime(value?.DeliveryOrderOn);
        Tariff = Tariffs.FirstOrDefault(t => t.Name == value?.Tariff);
        LoadCycles();
    }

    [RelayCommand]
    public void Refresh()
    {
        using var db = services.NewContext();
        Reload(Tariffs, db.DemurrageTariffs.OrderBy(t => t.Code).Select(t => new Lookup(t.Id, t.Code)).ToList());
        Reload(Depots, db.Depots.OrderBy(d => d.Name).Select(d => new Lookup(d.Id, d.Name)).ToList());
        var id = Selected?.Id;
        Reload(Items, db.BillsOfLading.OrderByDescending(b => b.Id).Take(1000)
            .Select(b => new BlRow(b.Id, b.HouseNo, b.Direction, b.Line!.Code, b.DeliveryOrderOn, b.DemurrageTariff!.Code,
                b.Cycles.Count(c => !c.IsDeleted)))
            .ToList());
        Selected = Items.FirstOrDefault(b => b.Id == id);
    }

    private void LoadCycles()
    {
        Cycles.Clear();
        if (Selected is null) return;
        using var db = services.NewContext();
        foreach (var c in db.ContainerCycles.Include(c => c.Container).Include(c => c.EmptyReturnDepot)
                     .Where(c => c.BillOfLadingId == Selected.Id).OrderBy(c => c.Container!.Number).ToList())
            Cycles.Add(new CycleRow(c.Id, c.Container!.Number, c.Container.SizeFeet, c.Container.TypeCode,
                ContainerRules.ToCode(c.CargoType), c.ReleasedToShipperOn, c.FullReturnedOn, c.DischargedOn,
                c.EmptyReturnedOn, c.EmptyReturnDepot?.Name));
    }

    [RelayCommand]
    private void Create()
    {
        if (string.IsNullOrWhiteSpace(NewHouseNo)) return;
        AppServices.Try(() =>
        {
            using var db = services.NewContext();
            db.BillsOfLading.Add(new BillOfLading
            {
                HouseNo = NewHouseNo.Trim(), Direction = NewDirection, DemurrageTariffId = Tariff?.Id,
                DeliveryOrderOn = AppServices.ToDate(DeliveryOrderDate),
            });
            db.SaveChanges();
            NewHouseNo = "";
        });
        Refresh();
    }

    [RelayCommand]
    private void SaveHeader()
    {
        if (Selected is null) return;
        AppServices.Try(() =>
        {
            using var db = services.NewContext();
            var bl = db.BillsOfLading.Single(b => b.Id == Selected.Id);
            bl.DemurrageTariffId = Tariff?.Id;
            bl.DeliveryOrderOn = AppServices.ToDate(DeliveryOrderDate);
            db.SaveChanges();
        });
        Refresh();
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is null) return;
        AppServices.Try(() =>
        {
            using var db = services.NewContext();
            var bl = db.BillsOfLading.Single(b => b.Id == Selected.Id);
            LifecycleRules.EnsureCanDelete(bl, db.LiveCycleCount(bl.Id));
            db.BillsOfLading.Remove(bl);
            db.SaveChanges();
        });
        Selected = null;
        Refresh();
    }

    [RelayCommand]
    private void SetDischarge() => OnCycle((db, c, _) => c.DischargedOn = AppServices.ToDate(EventDate));

    [RelayCommand]
    private void RecordEmptyReturn() => OnCycle((db, c, bl) =>
    {
        var settings = SettingsStore.Load(db);
        var depot = Depot is null ? null : db.Depots.Find(Depot.Id);
        LifecycleRules.RecordEmptyReturn(c, bl, AppServices.ToDate(EventDate) ?? throw new FormatException("Enter a date."),
            depot, null, db.IsCoveredByDemurrageInvoice(c.Id), allowOverwrite: true,
            requirePrerequisites: settings.EmptyReturnRequiresDischargeAndDo);
    });

    [RelayCommand]
    private void ClearEmptyReturn() => OnCycle((db, c, _) => LifecycleRules.ClearEmptyReturn(c, db.IsCoveredByDemurrageInvoice(c.Id)));

    [RelayCommand]
    private void RemoveContainer() => OnCycle((db, c, _) =>
    {
        if (db.IsCoveredByDemurrageInvoice(c.Id))
            throw new BusinessRuleException("BR-L4", "This container is on a demurrage invoice.");
        db.ContainerCycles.Remove(c);
    });

    private void OnCycle(Action<QuaysideDbContext, ContainerCycle, BillOfLading?> action)
    {
        if (SelectedCycle is null) return;
        AppServices.Try(() =>
        {
            using var db = services.NewContext();
            var c = db.ContainerCycles.Single(x => x.Id == SelectedCycle.Id);
            var bl = c.BillOfLadingId is { } id ? db.BillsOfLading.Find(id) : null;
            action(db, c, bl);
            db.SaveChanges();
        });
        Refresh();
    }

    private static void Reload<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var i in items) target.Add(i);
    }
}
