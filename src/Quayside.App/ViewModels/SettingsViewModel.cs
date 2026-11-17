using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quayside.Data;
using Quayside.Domain.Demurrage;
using Quayside.Domain.Finance;
using Quayside.Domain.Settings;

namespace Quayside.App.ViewModels;

public sealed partial class SettingItem : ObservableObject
{
    public required PropertyInfo Property { get; init; }
    public required SettingAttribute Meta { get; init; }
    public string Group => Meta.Group;
    public string Label => Meta.Label;
    public string Description => Meta.Description;
    public string Rule => Meta.Rule;
    public bool IsAssumption => Meta.IsAssumption;
    public bool IsBool => Property.PropertyType == typeof(bool);

    [ObservableProperty] private string _text = "";
    [ObservableProperty] private bool _flag;
}

public sealed partial class SettingsViewModel(AppServices services) : ObservableObject
{
    public ObservableCollection<SettingItem> Items { get; } = new();

    [ObservableProperty] private string _assumptionBanner = "";
    [ObservableProperty] private string _status = "";

    [RelayCommand]
    public void Refresh()
    {
        var s = services.LoadSettings();
        Items.Clear();
        foreach (var (p, meta) in QuaysideSettings.Describe())
        {
            var v = p.GetValue(s);
            Items.Add(new SettingItem
            {
                Property = p, Meta = meta, Flag = v is true,
                Text = v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v?.ToString() ?? "",
            });
        }
        var n = Items.Count(i => i.IsAssumption);
        AssumptionBanner = $"⚠ {n} settings use defaults that are assumptions, not confirmed rules. Review them before going live.";
        Status = $"Database: {services.Database.Provider}";
    }

    [RelayCommand]
    private void Save()
    {
        var ok = AppServices.Try(() =>
        {
            var s = new QuaysideSettings();
            foreach (var i in Items)
            {
                var t = i.Property.PropertyType;
                object? value = t == typeof(bool) ? i.Flag
                    : t == typeof(string) ? i.Text.Trim()
                    : t == typeof(int) ? int.Parse(i.Text, CultureInfo.InvariantCulture)
                    : t == typeof(int?) ? (string.IsNullOrWhiteSpace(i.Text) ? null : int.Parse(i.Text, CultureInfo.InvariantCulture))
                    : throw new FormatException($"Unsupported setting type for {i.Label}");
                i.Property.SetValue(s, value);
            }
            // validate composite values before saving
            _ = DemurrageOptions.From(s);
            _ = CurrencyRounding.Parse(s.CurrencyDecimals);
            using var db = services.NewContext();
            SettingsStore.Save(db, s);
        });
        Status = ok ? "Settings saved." : "Not saved.";
    }
}
