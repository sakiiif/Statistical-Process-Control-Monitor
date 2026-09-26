namespace SpcMonitor.Models;

/// Center line and sigma-zone boundaries for an Individuals (I-MR style) control chart,
/// derived once from a baseline sample and then held fixed while new points are evaluated
/// against it (the standard SPC approach: limits reflect the "in control" process).
public class ControlLimits
{
    public double CenterLine { get; init; }
    public double Sigma { get; init; }

    public double UCL => CenterLine + 3 * Sigma;
    public double LCL => CenterLine - 3 * Sigma;
    public double UpperZoneA => CenterLine + 2 * Sigma;
    public double LowerZoneA => CenterLine - 2 * Sigma;
    public double UpperZoneB => CenterLine + 1 * Sigma;
    public double LowerZoneB => CenterLine - 1 * Sigma;

    public bool IsEstablished => Sigma > 0;
}
