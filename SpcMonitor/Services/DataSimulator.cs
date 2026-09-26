namespace SpcMonitor.Services;

public enum SimulatedParameter
{
    ChamberTemperatureC,
    ChamberPressureMTorr,
    DepositionRateNmPerMin,
    RfPowerWatts
}

/// Generates synthetic sensor readings around a target mean with Gaussian noise.
/// Supports on-demand "drift" (a slow mean shift) and "spike" (a single outlier)
/// injections so the SPC rules have real events to catch during a demo.
public class DataSimulator
{
    private readonly Random _random = new();
    private double _driftOffset;
    private int _driftTicksRemaining;
    private double _driftPerTick;

    public SimulatedParameter Parameter { get; set; } = SimulatedParameter.ChamberTemperatureC;

    public (double Mean, double StdDev, string Unit) GetProfile() => Parameter switch
    {
        SimulatedParameter.ChamberTemperatureC => (350.0, 1.2, "°C"),
        SimulatedParameter.ChamberPressureMTorr => (5.0, 0.15, "mTorr"),
        SimulatedParameter.DepositionRateNmPerMin => (12.0, 0.4, "nm/min"),
        SimulatedParameter.RfPowerWatts => (1200.0, 8.0, "W"),
        _ => (0, 1, "")
    };

    /// Generates the next reading, applying any active drift.
    public double NextReading()
    {
        var (mean, stdDev, _) = GetProfile();
        double noise = SampleGaussian() * stdDev;

        if (_driftTicksRemaining > 0)
        {
            _driftOffset += _driftPerTick;
            _driftTicksRemaining--;
        }

        return mean + _driftOffset + noise;
    }

    /// Injects a single large outlier on the next reading only.
    public double SpikeReading()
    {
        var (mean, stdDev, _) = GetProfile();
        double direction = _random.NextDouble() > 0.5 ? 1 : -1;
        return mean + _driftOffset + direction * stdDev * 4.5;
    }

    /// Starts a gradual mean shift of <paramref name="totalSigmaShift"/> sigma over
    /// <paramref name="ticks"/> readings — enough to eventually trip Rule 4 (and often
    /// Rules 2/3 along the way).
    public void StartDrift(double totalSigmaShift, int ticks)
    {
        var (_, stdDev, _) = GetProfile();
        _driftTicksRemaining = ticks;
        _driftPerTick = (totalSigmaShift * stdDev) / ticks;
    }

    public void ClearDrift()
    {
        _driftOffset = 0;
        _driftTicksRemaining = 0;
        _driftPerTick = 0;
    }

    /// Generates a clean baseline sample (no drift/spikes) to establish control limits.
    public List<double> GenerateBaseline(int count)
    {
        var (mean, stdDev, _) = GetProfile();
        var values = new List<double>(count);
        for (int i = 0; i < count; i++)
            values.Add(mean + SampleGaussian() * stdDev);
        return values;
    }

    private double SampleGaussian()
    {
        // Box-Muller transform
        double u1 = 1.0 - _random.NextDouble();
        double u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
