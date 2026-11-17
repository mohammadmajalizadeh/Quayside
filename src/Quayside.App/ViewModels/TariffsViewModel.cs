using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Quayside.Domain.Demurrage;
using Quayside.Domain.Entities;
using Quayside.Domain.Rules;

namespace Quayside.App.ViewModels;

/// <summary>One slab in the editor grid: day range plus a daily rate per equipment class.</summary>
public sealed partial class SlabRow : ObservableObject
{
    [ObservableProperty] private int _fromDay = 1;
    [ObservableProperty] private int? _toDay;
    [ObservableProperty] private decimal? _rate20;
    [ObservableProperty] private decimal? _rate40;
    [ObservableProperty] private decimal? _rate40HC;
    [ObservableProperty] private decimal? _rate45;
    [ObservableProperty] private decimal? _rateReefer;

    public IEnumerable<(EquipmentClass, decimal?)> Rates() => new[]
    {
        (EquipmentClass.Dry20, Rate20), (EquipmentClass.Dry40, Rate40), (EquipmentClass.HighCube40, Rate40HC),
        (EquipmentClass.Dry45, Rate45), (EquipmentClass.Reefer, RateReefer),
    };
}

public sealed partial class TariffsViewModel(AppServices services) : ObservableObject
{
    public ObservableCollection<Lookup> Items { get; } = new();
    public ObservableCollection<SlabRow> Slabs { get; } = new();
    public DemurrageKind[] Kinds { get; } = Enum.GetValues<DemurrageKind>();

    [ObservableProperty] private Lookup? _selected;
    [ObservableProperty] private string _code = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private DemurrageKind _kind = DemurrageKind.ImportDemurrage;
    [ObservableProperty] private string _currency = "USD";
    [ObservableProperty] private int _freeDays;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _status = "";

    [RelayCommand]
    public void Refresh()
    {
        using var db = services.NewContext();
        Items.Clear();
        foreach (var t in db.DemurrageTariffs.OrderBy(t => t.Code).Select(t => new Lookup(t.Id, t.Code + " - " + t.Name)))
            Items.Add(t);
    }

    partial void OnSelectedChanged(Lookup? value)
    {
        Slabs.Clear();
        if (value is null) return;
        using var db = services.NewContext();
        var t = db.DemurrageTariffs.Include(x => x.Slabs).ThenInclude(s => s.Rates).Single(x => x.Id == value.Id);
        (Code, Name, Kind, Currency, FreeDays, IsActive) = (t.Code, t.Name, t.Kind, t.CurrencyCode, t.FreeDays, t.IsActive);
        foreach (var s in t.Slabs.OrderBy(s => s.FromDay))
        {
            decimal? R(EquipmentClass c) => s.Rates.FirstOrDefault(r => r.EquipmentClass == c)?.DailyRate;
            Slabs.Add(new SlabRow
            {
                FromDay = s.FromDay, ToDay = s.ToDay, Rate20 = R(EquipmentClass.Dry20), Rate40 = R(EquipmentClass.Dry40),
                Rate40HC = R(EquipmentClass.HighCube40), Rate45 = R(EquipmentClass.Dry45), RateReefer = R(EquipmentClass.Reefer),
            });
        }
        Status = "";
    }

    [RelayCommand]
    private void New()
    {
        Selected = null;
        (Code, Name, Kind, Currency, IsActive) = ("", "", DemurrageKind.ImportDemurrage, "USD", true);
        FreeDays = services.LoadSettings().DemurrageDefaultFreeDays;
        Slabs.Clear();
        Slabs.Add(new SlabRow { FromDay = FreeDays + 1 });
        Status = "New tariff: fill in the slabs, then Save.";
    }

    [RelayCommand]
    private void AddSlab()
    {
        var last = Slabs.LastOrDefault();
        Slabs.Add(new SlabRow { FromDay = last?.ToDay is { } t ? t + 1 : (last?.FromDay ?? FreeDays) + 1 });
    }

    [RelayCommand]
    private void RemoveSlab(SlabRow? row)
    {
        if (row is not null) Slabs.Remove(row);
    }

    [RelayCommand]
    private void Save()
    {
        var ok = AppServices.Try(() =>
        {
            using var db = services.NewContext();
            var t = Selected is null
                ? db.DemurrageTariffs.Add(new DemurrageTariff()).Entity
                : db.DemurrageTariffs.Include(x => x.Slabs).ThenInclude(s => s.Rates).Single(x => x.Id == Selected.Id);
            (t.Code, t.Name, t.Kind, t.CurrencyCode, t.FreeDays, t.IsActive) =
                (Code.Trim(), Name.Trim(), Kind, Currency.Trim().ToUpperInvariant(), FreeDays, IsActive);
            db.DemurrageSlabs.RemoveRange(t.Slabs);
            t.Slabs = Slabs.Select(s => new DemurrageSlab
            {
                FromDay = s.FromDay, ToDay = s.ToDay,
                Rates = s.Rates().Where(r => r.Item2 is not null)
                    .Select(r => new DemurrageRate { EquipmentClass = r.Item1, DailyRate = r.Item2!.Value }).ToList(),
            }).ToList();
            if (string.IsNullOrWhiteSpace(t.Code)) throw new BusinessRuleException("BR-D1", "Enter a tariff code.");
            var problems = DemurrageCalculator.ValidateTariff(t);
            if (problems.Count > 0) throw new BusinessRuleException(problems);
            db.SaveChanges();
        });
        if (ok)
        {
            Status = "Saved.";
            Refresh();
        }
    }
}
