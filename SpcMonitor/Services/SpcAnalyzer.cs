using SpcMonitor.Models;

namespace SpcMonitor.Services;

/// Establishes control limits from a baseline sample and evaluates each new point
/// against the four classic Western Electric rules. Designed for individual
/// (subgroup size = 1) measurements, which is the typical case for a single
/// continuous sensor feed (temperature, pressure, deposition rate, etc.).
public class SpcAnalyzer
{
    private const int MinBaselineSize = 15;

    public ControlLimits Limits { get; private set; } = new();

    /// Computes the center line and sigma from a baseline window using the
    /// average moving range method (MRbar / 1.128), which is the standard
    /// way to estimate process sigma for individuals charts without assuming
    /// subgrouping.
    public void EstablishBaseline(IReadOnlyList<double> baselineValues)
    {
        if (baselineValues.Count < MinBaselineSize)
            throw new InvalidOperationException($"Need at least {MinBaselineSize} points to establish a baseline.");

        double mean = baselineValues.Average();

        double sumMovingRange = 0;
        for (int i = 1; i < baselineValues.Count; i++)
            sumMovingRange += Math.Abs(baselineValues[i] - baselineValues[i - 1]);

        double meanMovingRange = sumMovingRange / (baselineValues.Count - 1);
        double sigma = meanMovingRange / 1.128; // d2 constant for n=2 moving range

        Limits = new ControlLimits { CenterLine = mean, Sigma = sigma };
    }

    public void Reset() => Limits = new ControlLimits();

    /// Evaluates the newest point against the trailing window (including itself)
    /// and returns any rule violations it triggers. Call this after appending
    /// the new measurement to your history and assigning its Zone.
    public List<Violation> Evaluate(IReadOnlyList<Measurement> history)
    {
        var violations = new List<Violation>();
        if (!Limits.IsEstablished || history.Count == 0)
            return violations;

        var current = history[^1];
        AssignZone(current);

        // Rule 1: single point beyond 3 sigma (UCL/LCL)
        if (current.Zone == SigmaZone.Beyond)
        {
            violations.Add(new Violation
            {
                Sequence = current.Sequence,
                Timestamp = current.Timestamp,
                Value = current.Value,
                RuleName = "Rule 1",
                Description = "Point beyond 3σ control limit"
            });
        }

        // Rule 2: 2 of 3 consecutive points beyond 2 sigma, same side
        if (history.Count >= 3)
        {
            var last3 = history.TakeLast(3).ToList();
            if (CountBeyondZoneASameSide(last3, out int sideCount, out int side) && sideCount >= 2)
            {
                violations.Add(new Violation
                {
                    Sequence = current.Sequence,
                    Timestamp = current.Timestamp,
                    Value = current.Value,
                    RuleName = "Rule 2",
                    Description = $"2 of 3 points beyond 2σ on {(side > 0 ? "upper" : "lower")} side"
                });
            }
        }

        // Rule 3: 4 of 5 consecutive points beyond 1 sigma, same side
        if (history.Count >= 5)
        {
            var last5 = history.TakeLast(5).ToList();
            if (CountBeyondZoneBSameSide(last5, out int sideCount, out int side) && sideCount >= 4)
            {
                violations.Add(new Violation
                {
                    Sequence = current.Sequence,
                    Timestamp = current.Timestamp,
                    Value = current.Value,
                    RuleName = "Rule 3",
                    Description = $"4 of 5 points beyond 1σ on {(side > 0 ? "upper" : "lower")} side"
                });
            }
        }

        // Rule 4: 8 consecutive points on the same side of the center line
        if (history.Count >= 8)
        {
            var last8 = history.TakeLast(8).ToList();
            bool allAbove = last8.All(m => m.Value > Limits.CenterLine);
            bool allBelow = last8.All(m => m.Value < Limits.CenterLine);
            if (allAbove || allBelow)
            {
                violations.Add(new Violation
                {
                    Sequence = current.Sequence,
                    Timestamp = current.Timestamp,
                    Value = current.Value,
                    RuleName = "Rule 4",
                    Description = $"8 consecutive points on {(allAbove ? "upper" : "lower")} side of center line"
                });
            }
        }

        current.IsViolation = violations.Count > 0;
        return violations;
    }

    private void AssignZone(Measurement m)
    {
        double distance = Math.Abs(m.Value - Limits.CenterLine);
        m.Zone = distance switch
        {
            var d when d > 3 * Limits.Sigma => SigmaZone.Beyond,
            var d when d > 2 * Limits.Sigma => SigmaZone.WithinA,
            var d when d > 1 * Limits.Sigma => SigmaZone.WithinB,
            _ => SigmaZone.WithinC
        };
    }

    private bool CountBeyondZoneASameSide(List<Measurement> points, out int count, out int side)
    {
        int above = points.Count(p => p.Value - Limits.CenterLine > 2 * Limits.Sigma);
        int below = points.Count(p => Limits.CenterLine - p.Value > 2 * Limits.Sigma);
        if (above >= below) { count = above; side = 1; return above > 0; }
        count = below; side = -1; return below > 0;
    }

    private bool CountBeyondZoneBSameSide(List<Measurement> points, out int count, out int side)
    {
        int above = points.Count(p => p.Value - Limits.CenterLine > 1 * Limits.Sigma);
        int below = points.Count(p => Limits.CenterLine - p.Value > 1 * Limits.Sigma);
        if (above >= below) { count = above; side = 1; return above > 0; }
        count = below; side = -1; return below > 0;
    }
}
