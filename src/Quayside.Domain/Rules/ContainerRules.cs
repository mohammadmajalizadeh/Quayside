using Quayside.Domain.Entities;

namespace Quayside.Domain.Rules;

/// <summary>Container and cargo validation rules (SPEC BR-C2..BR-C5, BR-C8).</summary>
public static class ContainerRules
{
    public static bool HasDangerousGoodsData(ContainerCycle c) =>
        !string.IsNullOrWhiteSpace(c.DgClass) || !string.IsNullOrWhiteSpace(c.UnNumber);

    /// <summary>
    /// BR-C3 / BR-C4. Overweight threshold: the container's max gross weight if known, otherwise
    /// <paramref name="fallbackOverweightKg"/> (null = no fallback check).
    /// </summary>
    public static IReadOnlyList<RuleViolation> ValidateCargoType(ContainerCycle cycle, Container? container,
        int? fallbackOverweightKg = null)
    {
        var v = new List<RuleViolation>();
        var dg = HasDangerousGoodsData(cycle);
        if (cycle.CargoType == CargoType.Standard && dg)
            v.Add(new("BR-C3", "Cargo type ST is not allowed when a DG class or UN number is given."));
        if (cycle.CargoType == CargoType.Dangerous && !dg)
            v.Add(new("BR-C3", "Cargo type DG requires a DG class or UN number."));

        var limit = container?.MaxGrossKg ?? fallbackOverweightKg;
        if (limit is > 0 && cycle.GrossWeightKg > limit && cycle.CargoType != CargoType.OutOfGauge)
            v.Add(new("BR-C4", $"Gross weight {cycle.GrossWeightKg:0.###} kg exceeds {limit} kg; cargo type must be OG."));
        return v;
    }

    /// <summary>BR-C2: container owner prefix must be permitted by the line (if the line restricts prefixes).</summary>
    public static IReadOnlyList<RuleViolation> ValidateOwnerPrefix(string normalizedNumber, ShippingLine? line)
    {
        var prefixes = line?.PrefixList() ?? Array.Empty<string>();
        if (prefixes.Count == 0 || prefixes.Any(p => normalizedNumber.StartsWith(p, StringComparison.Ordinal)))
            return Array.Empty<RuleViolation>();
        return new[]
        {
            new RuleViolation("BR-C2",
                $"Container {normalizedNumber} must start with one of: {string.Join(", ", prefixes)} (line {line!.Code})."),
        };
    }

    /// <summary>BR-C5: duplicates within one batch / one bill of lading. Returns the duplicated numbers.</summary>
    public static IReadOnlyList<string> FindDuplicates(IEnumerable<string> normalizedNumbers) =>
        normalizedNumbers.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

    /// <summary>BR-C8: imported size/type must match the equipment master.</summary>
    public static IReadOnlyList<RuleViolation> ValidateAgainstMaster(Container master, int? sizeFeet, string? typeCode)
    {
        var v = new List<RuleViolation>();
        if (sizeFeet is not null && sizeFeet != master.SizeFeet)
            v.Add(new("BR-C8", $"Container {master.Number} is registered as {master.SizeFeet}' but the file says {sizeFeet}'."));
        if (!string.IsNullOrWhiteSpace(typeCode) &&
            !string.Equals(typeCode.Trim(), master.TypeCode, StringComparison.OrdinalIgnoreCase))
            v.Add(new("BR-C8", $"Container {master.Number} is registered as type {master.TypeCode} but the file says {typeCode}."));
        return v;
    }

    public static CargoType ParseCargoType(string? code) => (code ?? "").Trim().ToUpperInvariant() switch
    {
        "" or "ST" => CargoType.Standard,
        "DG" => CargoType.Dangerous,
        "OG" => CargoType.OutOfGauge,
        _ => throw new FormatException($"Unknown cargo type '{code}' (expected ST, DG or OG)."),
    };

    public static string ToCode(CargoType t) => t switch
    {
        CargoType.Dangerous => "DG",
        CargoType.OutOfGauge => "OG",
        _ => "ST",
    };
}
