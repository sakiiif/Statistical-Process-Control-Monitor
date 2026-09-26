namespace SpcMonitor.Models;

/// A flagged Western Electric rule violation, referencing the measurement that triggered it.
public class Violation
{
    public int Sequence { get; init; }
    public DateTime Timestamp { get; init; }
    public double Value { get; init; }
    public string RuleName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
