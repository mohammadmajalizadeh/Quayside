using System.Globalization;
using Quayside.Domain.Entities;
using Quayside.Domain.Settings;

namespace Quayside.Data;

/// <summary>Loads/saves <see cref="QuaysideSettings"/> from the AppSettings table. Unknown keys are ignored.</summary>
public static class SettingsStore
{
    public static QuaysideSettings Load(QuaysideDbContext db)
    {
        var s = new QuaysideSettings();
        var stored = db.Settings.ToDictionary(x => x.Key, x => x.Value);
        foreach (var (p, _) in QuaysideSettings.Describe())
        {
            if (!stored.TryGetValue(p.Name, out var raw)) continue;
            p.SetValue(s, Parse(raw, p.PropertyType));
        }
        return s;
    }

    public static void Save(QuaysideDbContext db, QuaysideSettings s)
    {
        var existing = db.Settings.ToDictionary(x => x.Key);
        foreach (var (p, _) in QuaysideSettings.Describe())
        {
            var value = Format(p.GetValue(s));
            if (existing.TryGetValue(p.Name, out var row)) row.Value = value;
            else db.Settings.Add(new AppSetting { Key = p.Name, Value = value });
        }
        db.SaveChanges();
    }

    private static string Format(object? v) => v switch
    {
        null => "",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? "",
    };

    private static object? Parse(string raw, Type t)
    {
        var u = Nullable.GetUnderlyingType(t);
        if (u is not null) return raw.Length == 0 ? null : Parse(raw, u);
        if (t == typeof(string)) return raw;
        if (t == typeof(bool)) return bool.Parse(raw);
        if (t == typeof(int)) return int.Parse(raw, CultureInfo.InvariantCulture);
        if (t == typeof(decimal)) return decimal.Parse(raw, CultureInfo.InvariantCulture);
        throw new NotSupportedException($"Setting type {t}");
    }
}
