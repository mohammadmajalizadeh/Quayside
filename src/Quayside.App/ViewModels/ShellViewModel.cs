using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quayside.Domain.Entities;

namespace Quayside.App.ViewModels;

public sealed record PartyRow(int Id, string Code, string NameEn, string? NameFa, PartyRole Roles);
public sealed record LineRow(int Id, string Code, string Name, string? OwnerPrefixes);

public sealed partial class ReferenceViewModel(AppServices services) : ObservableObject
{
    public ObservableCollection<PartyRow> Parties { get; } = new();
    public ObservableCollection<LineRow> Lines { get; } = new();
    public ObservableCollection<Lookup> Depots { get; } = new();

    [ObservableProperty] private string _partyCode = "";
    [ObservableProperty] private string _partyName = "";
    [ObservableProperty] private string _partyNameFa = "";
    [ObservableProperty] private string _lineCode = "";
    [ObservableProperty] private string _lineName = "";
    [ObservableProperty] private string _linePrefixes = "";
    [ObservableProperty] private string _depotName = "";

    [RelayCommand]
    public void Refresh()
    {
        using var db = services.NewContext();
        Parties.Clear();
        foreach (var p in db.Parties.OrderBy(p => p.Code).Select(p => new PartyRow(p.Id, p.Code, p.NameEn, p.NameFa, p.Roles))) Parties.Add(p);
        Lines.Clear();
        foreach (var l in db.Lines.OrderBy(l => l.Code).Select(l => new LineRow(l.Id, l.Code, l.Name, l.OwnerPrefixes))) Lines.Add(l);
        Depots.Clear();
        foreach (var d in db.Depots.OrderBy(d => d.Name).Select(d => new Lookup(d.Id, d.Name))) Depots.Add(d);
    }

    [RelayCommand]
    private void AddParty() => Add(() => new Party
    {
        Code = PartyCode.Trim(), NameEn = PartyName.Trim(), NameFa = NullIfEmpty(PartyNameFa), Roles = PartyRole.Client,
    }, PartyCode, () => (PartyCode, PartyName, PartyNameFa) = ("", "", ""));

    [RelayCommand]
    private void AddLine() => Add(() => new ShippingLine
    {
        Code = LineCode.Trim().ToUpperInvariant(), Name = LineName.Trim(), OwnerPrefixes = NullIfEmpty(LinePrefixes),
    }, LineCode, () => (LineCode, LineName, LinePrefixes) = ("", "", ""));

    [RelayCommand]
    private void AddDepot() => Add(() => new Depot { Name = DepotName.Trim() }, DepotName, () => DepotName = "");

    private void Add(Func<Entity> create, string key, Action clear)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (AppServices.Try(() =>
            {
                using var db = services.NewContext();
                db.Add(create());
                db.SaveChanges();
            }))
            clear();
        Refresh();
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public sealed class ShellViewModel
{
    public ShellViewModel(AppServices services)
    {
        Containers = new ContainersViewModel(services);
        Bills = new BillsViewModel(services);
        Import = new ImportViewModel(services);
        Tariffs = new TariffsViewModel(services);
        Demurrage = new DemurrageViewModel(services);
        Reference = new ReferenceViewModel(services);
        Settings = new SettingsViewModel(services);
        RefreshAll = new RelayCommand(Refresh);
        Refresh();
    }

    public ContainersViewModel Containers { get; }
    public BillsViewModel Bills { get; }
    public ImportViewModel Import { get; }
    public TariffsViewModel Tariffs { get; }
    public DemurrageViewModel Demurrage { get; }
    public ReferenceViewModel Reference { get; }
    public SettingsViewModel Settings { get; }
    public IRelayCommand RefreshAll { get; }

    private void Refresh()
    {
        Containers.Refresh();
        Bills.Refresh();
        Import.Refresh();
        Tariffs.Refresh();
        Demurrage.Refresh();
        Reference.Refresh();
        Settings.Refresh();
    }
}
