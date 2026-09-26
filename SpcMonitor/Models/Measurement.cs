namespace SpcMonitor.Models;

/// A single sensor reading captured at a point in time for a given process parameter.
public class Measurement
{
    public int Sequence { get; init; }
    public DateTime Timestamp { get; init; }
    public double Value { get; init; }

    /// Which control-zone the point falls in relative to the current limits (set by SpcAnalyzer).
    public SigmaZone Zone { get; set; } = SigmaZone.WithinC;

    /// True if this point triggered any Western Electric rule.
    public bool IsViolation { get; set; }
}

public enum SigmaZone
{
    WithinC,   // within 1 sigma of center line
    WithinB,   // 1-2 sigma
    WithinA,   // 2-3 sigma
    Beyond     // beyond 3 sigma (out of control limits)
}
