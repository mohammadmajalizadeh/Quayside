namespace Quayside.Domain.Rules;

public enum Severity { Warning = 1, Error = 2 }

/// <param name="Rule">SPEC rule id, e.g. "BR-C3".</param>
public sealed record RuleViolation(string Rule, string Message, Severity Severity = Severity.Error)
{
    public override string ToString() => $"[{Rule}] {Severity}: {Message}";
}

public static class RuleViolationExtensions
{
    public static bool HasErrors(this IEnumerable<RuleViolation> v) => v.Any(x => x.Severity == Severity.Error);
}

/// <summary>Thrown when an operation is refused by a business rule.</summary>
public sealed class BusinessRuleException : Exception
{
    public IReadOnlyList<RuleViolation> Violations { get; }

    public BusinessRuleException(IReadOnlyList<RuleViolation> violations)
        : base(string.Join("; ", violations.Select(v => v.ToString()))) => Violations = violations;

    public BusinessRuleException(string rule, string message) : this(new[] { new RuleViolation(rule, message) }) { }
}
