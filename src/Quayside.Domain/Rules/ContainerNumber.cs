using System.Text.RegularExpressions;

namespace Quayside.Domain.Rules;

/// <summary>ISO 6346 container number handling (SPEC BR-C1).</summary>
public static partial class ContainerNumber
{
    [GeneratedRegex("^[A-Z]{4}[0-9]{7}$")]
    private static partial Regex Format();

    /// <summary>Upper-cases and removes separators commonly typed before the check digit.</summary>
    public static string Normalize(string? raw) =>
        new string((raw ?? "").Where(c => !char.IsWhiteSpace(c) && c is not '-' and not '/' and not '.').ToArray())
            .ToUpperInvariant();

    /// <summary>ISO 6346 check digit for the first 10 characters (owner code + category + serial).</summary>
    public static int ComputeCheckDigit(string first10)
    {
        if (first10.Length != 10) throw new ArgumentException("Need 10 characters", nameof(first10));
        var sum = 0;
        for (var i = 0; i < 10; i++)
            sum += CharValue(first10[i]) * (1 << i);
        return sum % 11 % 10;
    }

    private static int CharValue(char c)
    {
        if (char.IsDigit(c)) return c - '0';
        // Letters start at 10 and skip multiples of 11.
        var v = 10;
        for (var l = 'A'; l < c; l++)
        {
            v++;
            if (v % 11 == 0) v++;
        }
        return v;
    }

    /// <summary>
    /// Validates a raw container number. Format problems are errors; check-digit and category
    /// problems are warnings unless <paramref name="checkDigitIsError"/> (SPEC OQ-14).
    /// </summary>
    public static IReadOnlyList<RuleViolation> Validate(string? raw, out string normalized, bool checkDigitIsError = false)
    {
        normalized = Normalize(raw);
        var issues = new List<RuleViolation>();
        if (!Format().IsMatch(normalized))
        {
            issues.Add(new("BR-C1", $"Container number '{raw}' must be 4 letters followed by 7 digits."));
            return issues;
        }
        if (normalized[3] is not ('U' or 'J' or 'Z'))
            issues.Add(new("BR-C1", $"Container number '{normalized}': category letter should be U, J or Z.", Severity.Warning));
        var expected = ComputeCheckDigit(normalized[..10]);
        if (normalized[10] - '0' != expected)
            issues.Add(new("BR-C1", $"Container number '{normalized}': check digit should be {expected}.",
                checkDigitIsError ? Severity.Error : Severity.Warning));
        return issues;
    }
}
