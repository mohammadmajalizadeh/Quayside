using Quayside.Domain.Entities;
using Quayside.Domain.Rules;
using Quayside.Domain.Settings;

namespace Quayside.Domain.Demurrage;

/// <summary>Day-counting and classification options, normally built from <see cref="QuaysideSettings"/>.</summary>
public sealed record DemurrageOptions
{
    public bool CountStartDay { get; init; } = true;
    public bool CountEndDay { get; init; } = true;
    public IReadOnlyList<string> ReeferTypeCodes { get; init; } = new[] { "RF", "RH", "RE" };
    public IReadOnlyList<string> HighCubeTypeCodes { get; init; } = new[] { "HC", "HQ" };
    public IReadOnlyDictionary<EquipmentClass, EquipmentClass> RateFallbacks { get; init; } =
        new Dictionary<EquipmentClass, EquipmentClass>();

    public static DemurrageOptions From(QuaysideSettings s) => new()
    {
        CountStartDay = s.DemurrageCountStartDay,
        CountEndDay = s.DemurrageCountEndDay,
        ReeferTypeCodes = QuaysideSettings.SplitCodes(s.DemurrageReeferTypeCodes),
        HighCubeTypeCodes = QuaysideSettings.SplitCodes(s.DemurrageHighCubeTypeCodes),
        RateFallbacks = ParseFallbacks(s.DemurrageRateFallbacks),
    };

    /// <summary>Parses "HighCube40=Dry40, Dry45=Dry40".</summary>
    public static IReadOnlyDictionary<EquipmentClass, EquipmentClass> ParseFallbacks(string? text)
    {
        var map = new Dictionary<EquipmentClass, EquipmentClass>();
        foreach (var pair in (text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split('=', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !Enum.TryParse<EquipmentClass>(parts[0], true, out var from) ||
                !Enum.TryParse<EquipmentClass>(parts[1], true, out var to))
                throw new FormatException($"Invalid rate fallback '{pair}'. Use e.g. HighCube40=Dry40.");
            map[from] = to;
        }
        return map;
    }
}

public sealed record DemurrageLine(int FromDay, int ToDay, int Days, decimal DailyRate, EquipmentClass RateClass)
{
    public decimal Amount => Days * DailyRate;
}

public sealed record DemurrageResult(
    DateOnly Start, DateOnly End, EquipmentClass EquipmentClass, int TotalDays, int FreeDays,
    IReadOnlyList<DemurrageLine> Lines, string CurrencyCode)
{
    public int ChargeableDays => Lines.Sum(l => l.Days);
    public decimal Total => Lines.Sum(l => l.Amount);
}

public static class DemurrageCalculator
{
    /// <summary>Maps container size + ISO type group to the rate class (BR-D3).</summary>
    public static EquipmentClass Classify(int sizeFeet, string? typeCode, DemurrageOptions o)
    {
        var type = (typeCode ?? "").Trim().ToUpperInvariant();
        if (o.ReeferTypeCodes.Contains(type)) return EquipmentClass.Reefer;
        return sizeFeet switch
        {
            20 => EquipmentClass.Dry20,
            40 when o.HighCubeTypeCodes.Contains(type) => EquipmentClass.HighCube40,
            40 => EquipmentClass.Dry40,
            45 => EquipmentClass.Dry45,
            _ => throw new BusinessRuleException("BR-D3", $"No demurrage class for a {sizeFeet}' {type} container."),
        };
    }

    /// <summary>Number of days in the period under the configured inclusivity (BR-D2).</summary>
    public static int CountDays(DateOnly start, DateOnly end, DemurrageOptions o)
    {
        if (end < start)
            throw new BusinessRuleException("BR-D2", $"End date {end:yyyy-MM-dd} is before start date {start:yyyy-MM-dd}.");
        var days = end.DayNumber - start.DayNumber + 1;
        if (!o.CountStartDay) days--;
        if (!o.CountEndDay) days--;
        return Math.Max(0, days);
    }

    /// <summary>Validates tariff structure (BR-D1). Returns all problems found.</summary>
    public static IReadOnlyList<RuleViolation> ValidateTariff(DemurrageTariff t)
    {
        var v = new List<RuleViolation>();
        if (t.FreeDays < 0) v.Add(new("BR-D1", "Free days cannot be negative."));
        var slabs = t.Slabs.OrderBy(s => s.FromDay).ToList();
        if (slabs.Count == 0)
        {
            v.Add(new("BR-D1", "Define at least one rate slab."));
            return v;
        }
        if (slabs[0].FromDay < 1 || slabs[0].FromDay > t.FreeDays + 1)
            v.Add(new("BR-D1", $"The first slab must start between day 1 and day {t.FreeDays + 1} (the first chargeable day)."));
        for (var i = 0; i < slabs.Count; i++)
        {
            var s = slabs[i];
            var last = i == slabs.Count - 1;
            if (s.ToDay is null && !last)
                v.Add(new("BR-D1", $"Only the last slab may be open-ended (slab from day {s.FromDay})."));
            if (s.ToDay is { } to && to < s.FromDay)
                v.Add(new("BR-D1", $"Slab from day {s.FromDay} ends before it starts ({to})."));
            if (last && s.ToDay is not null)
                v.Add(new("BR-D1", "The last slab must be open-ended (leave 'to day' empty)."));
            if (!last && s.ToDay is { } end && slabs[i + 1].FromDay != end + 1)
                v.Add(new("BR-D1", $"Slabs must be contiguous: day {end + 1} expected after slab ending on day {end}, found {slabs[i + 1].FromDay}."));
            if (s.Rates.Any(r => r.DailyRate < 0))
                v.Add(new("BR-D1", $"Slab from day {s.FromDay} has a negative rate."));
            foreach (var dup in s.Rates.GroupBy(r => r.EquipmentClass).Where(g => g.Count() > 1))
                v.Add(new("BR-D1", $"Slab from day {s.FromDay} has more than one rate for {dup.Key}."));
        }
        return v;
    }

    /// <summary>
    /// Calculates the charge for one container between <paramref name="start"/> and <paramref name="end"/>
    /// (BR-D2, BR-D3). Days 1..FreeDays are free; each further day is charged at the rate of the slab
    /// containing that day number, for the container's equipment class (with optional fallbacks).
    /// </summary>
    public static DemurrageResult Calculate(DemurrageTariff tariff, DateOnly start, DateOnly end,
        EquipmentClass equipmentClass, DemurrageOptions options)
    {
        var problems = ValidateTariff(tariff);
        if (problems.Count > 0) throw new BusinessRuleException(problems);

        var total = CountDays(start, end, options);
        var slabs = tariff.Slabs.OrderBy(s => s.FromDay).ToList();
        var lines = new List<DemurrageLine>();
        var firstCharged = tariff.FreeDays + 1;

        foreach (var slab in slabs)
        {
            var from = Math.Max(slab.FromDay, firstCharged);
            var to = Math.Min(slab.ToDay ?? int.MaxValue, total);
            if (from > to) continue;
            var (rate, rateClass) = ResolveRate(slab, equipmentClass, options);
            lines.Add(new DemurrageLine(from, to, to - from + 1, rate, rateClass));
        }
        return new DemurrageResult(start, end, equipmentClass, total, Math.Min(tariff.FreeDays, total), lines,
            tariff.CurrencyCode);
    }

    private static (decimal, EquipmentClass) ResolveRate(DemurrageSlab slab, EquipmentClass cls, DemurrageOptions o)
    {
        var seen = new HashSet<EquipmentClass>();
        var current = cls;
        while (seen.Add(current))
        {
            var r = slab.Rates.FirstOrDefault(x => x.EquipmentClass == current);
            if (r is not null) return (r.DailyRate, current);
            if (!o.RateFallbacks.TryGetValue(current, out current)) break;
        }
        throw new BusinessRuleException("BR-D3",
            $"Slab from day {slab.FromDay} has no rate for {cls} and no fallback is configured.");
    }
}
