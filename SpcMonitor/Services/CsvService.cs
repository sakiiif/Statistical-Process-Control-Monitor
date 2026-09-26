using System.Globalization;
using System.IO;
using SpcMonitor.Models;

namespace SpcMonitor.Services;

/// Minimal CSV import/export for measurement runs and violation logs.
/// No external dependency needed for this simple two/four-column format.
public static class CsvService
{
    public static void ExportMeasurements(string path, IEnumerable<Measurement> measurements)
    {
        using var writer = new StreamWriter(path, append: false);
        writer.WriteLine("Sequence,Timestamp,Value,Zone,IsViolation");
        foreach (var m in measurements)
        {
            writer.WriteLine(string.Join(",",
                m.Sequence,
                m.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                m.Value.ToString("F4", CultureInfo.InvariantCulture),
                m.Zone,
                m.IsViolation));
        }
    }

    public static void ExportViolations(string path, IEnumerable<Violation> violations)
    {
        using var writer = new StreamWriter(path, append: false);
        writer.WriteLine("Sequence,Timestamp,Value,Rule,Description");
        foreach (var v in violations)
        {
            writer.WriteLine(string.Join(",",
                v.Sequence,
                v.Timestamp.ToString("o", CultureInfo.InvariantCulture),
                v.Value.ToString("F4", CultureInfo.InvariantCulture),
                v.RuleName,
                $"\"{v.Description}\""));
        }
    }

    /// Imports a simple single-column (or first-column) CSV of numeric readings,
    /// skipping a header row if the first line isn't numeric. Useful for feeding
    /// in a real/public SPC dataset instead of the simulator.
    public static List<double> ImportValues(string path)
    {
        var values = new List<double>();
        var lines = File.ReadAllLines(path);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var firstField = line.Split(',')[0].Trim();
            if (double.TryParse(firstField, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                values.Add(value);
        }
        return values;
    }
}
