using System.Reflection;

namespace Quayside.Domain.Settings;

/// <summary>Describes a setting for the settings screen.</summary>
/// <param name="Rule">Related SPEC rule/open question.</param>
/// <param name="IsAssumption">
/// True when the default is an assumption that was not confirmed from the original system or by
/// the product owner. The UI flags these so users review them.
/// </param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingAttribute(string group, string label, string description, string rule, bool isAssumption)
    : Attribute
{
    public string Group { get; } = group;
    public string Label { get; } = label;
    public string Description { get; } = description;
    public string Rule { get; } = rule;
    public bool IsAssumption { get; } = isAssumption;
}

/// <summary>All user-configurable behaviour. Persisted as key/value pairs (key = property name).</summary>
public sealed class QuaysideSettings
{
    // ---- Containers ----
    [Setting("Containers", "Wrong check digit is an error",
        "If off, an ISO 6346 check-digit mismatch is only a warning.", "BR-C1 / OQ-14", isAssumption: true)]
    public bool ContainerCheckDigitIsError { get; set; }

    [Setting("Containers", "Fallback overweight limit (kg)",
        "Used for the OG cargo-type check when a container has no max gross weight. Empty = no check.",
        "BR-C4 / OQ-15", isAssumption: true)]
    public int? OverweightFallbackKg { get; set; }

    // ---- Lifecycle ----
    [Setting("Lifecycle", "Empty return requires discharge and D/O dates",
        "Refuse an empty return unless the container has a discharge date and its B/L has a delivery-order date.",
        "BR-L2", isAssumption: false)]
    public bool EmptyReturnRequiresDischargeAndDo { get; set; } = true;

    // ---- Demurrage ----
    [Setting("Demurrage", "Count the start day",
        "The day of discharge (import) or release (export) counts as day 1.", "BR-D2 / OQ-16", isAssumption: true)]
    public bool DemurrageCountStartDay { get; set; } = true;

    [Setting("Demurrage", "Count the end day",
        "The day of return counts as a chargeable day.", "BR-D2 / OQ-16", isAssumption: true)]
    public bool DemurrageCountEndDay { get; set; } = true;

    [Setting("Demurrage", "Default free days",
        "Free days pre-filled on new tariffs. Each tariff's own free-days value is what the calculation uses.",
        "BR-D1 / OQ-16", isAssumption: true)]
    public int DemurrageDefaultFreeDays { get; set; }

    [Setting("Demurrage", "Reefer types",
        "Container type codes billed at the Reefer rate regardless of size (comma separated).",
        "BR-D3 / OQ-17", isAssumption: true)]
    public string DemurrageReeferTypeCodes { get; set; } = "RF,RH,RE";

    [Setting("Demurrage", "High-cube types",
        "40' container type codes billed at the 40HC rate (comma separated).", "BR-D3 / OQ-17", isAssumption: true)]
    public string DemurrageHighCubeTypeCodes { get; set; } = "HC,HQ";

    [Setting("Demurrage", "Rate fallbacks",
        "When a slab has no rate for a class, use another class's rate, e.g. 'HighCube40=Dry40,Dry45=Dry40'. Empty = missing rate is an error.",
        "BR-D3 / OQ-17", isAssumption: true)]
    public string DemurrageRateFallbacks { get; set; } = "";

    // ---- Display ----
    [Setting("Display", "Show Jalali dates",
        "Display dates in the Persian (Solar Hijri) calendar in addition to Gregorian.", "BR-G4 / OQ-13", isAssumption: false)]
    public bool ShowJalaliDates { get; set; }

    public static IReadOnlyList<(PropertyInfo Property, SettingAttribute Meta)> Describe() =>
        typeof(QuaysideSettings).GetProperties()
            .Select(p => (p, p.GetCustomAttribute<SettingAttribute>()))
            .Where(x => x.Item2 is not null)
            .Select(x => (x.p, x.Item2!))
            .ToList();

    public static IReadOnlyList<string> SplitCodes(string? csv) =>
        (csv ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToUpperInvariant()).ToList();
}
