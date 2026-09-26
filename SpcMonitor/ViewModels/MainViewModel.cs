using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using SpcMonitor.Models;
using SpcMonitor.Services;

namespace SpcMonitor.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DataSimulator _simulator = new();
    private readonly SpcAnalyzer _analyzer = new();
    private readonly DispatcherTimer _timer;
    private readonly List<Measurement> _history = new();
    private int _sequence;

    public ObservableCollection<Violation> Violations { get; } = new();
    public PlotModel ChartModel { get; }

    private LineSeries _valueSeries = null!;
    private LineAnnotation _clLine = null!, _uclLine = null!, _lclLine = null!;
    private LineAnnotation _upperA = null!, _lowerA = null!, _upperB = null!, _lowerB = null!;

    public IEnumerable<SimulatedParameter> AvailableParameters => Enum.GetValues<SimulatedParameter>();

    private SimulatedParameter _selectedParameter = SimulatedParameter.ChamberTemperatureC;
    public SimulatedParameter SelectedParameter
    {
        get => _selectedParameter;
        set
        {
            if (_selectedParameter == value) return;
            _selectedParameter = value;
            _simulator.Parameter = value;
            OnPropertyChanged();
            ResetRun();
        }
    }

    private string _statusText = "Establish a baseline to begin.";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            _isRunning = value;
            OnPropertyChanged();
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _baselineEstablished;
    public bool BaselineEstablished
    {
        get => _baselineEstablished;
        set
        {
            _baselineEstablished = value;
            OnPropertyChanged();
            StartCommand.RaiseCanExecuteChanged();
            SpikeCommand.RaiseCanExecuteChanged();
            DriftCommand.RaiseCanExecuteChanged();
        }
    }

    private string _violationCountText = "0 violations";
    public string ViolationCountText
    {
        get => _violationCountText;
        set { _violationCountText = value; OnPropertyChanged(); }
    }

    public RelayCommand EstablishBaselineCommand { get; }
    public RelayCommand ImportBaselineCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand SpikeCommand { get; }
    public RelayCommand DriftCommand { get; }
    public RelayCommand ExportCsvCommand { get; }
    public RelayCommand ResetCommand { get; }

    public MainViewModel()
    {
        ChartModel = BuildChartModel();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _timer.Tick += (_, _) => AddReading(_simulator.NextReading());

        EstablishBaselineCommand = new RelayCommand(EstablishBaselineFromSimulator);
        ImportBaselineCommand = new RelayCommand(EstablishBaselineFromCsv);
        StartCommand = new RelayCommand(() => { IsRunning = true; _timer.Start(); }, () => BaselineEstablished && !IsRunning);
        StopCommand = new RelayCommand(() => { IsRunning = false; _timer.Stop(); }, () => IsRunning);
        SpikeCommand = new RelayCommand(() => AddReading(_simulator.SpikeReading()), () => BaselineEstablished);
        DriftCommand = new RelayCommand(() => _simulator.StartDrift(totalSigmaShift: 3.5, ticks: 20), () => BaselineEstablished);
        ExportCsvCommand = new RelayCommand(ExportCsv);
        ResetCommand = new RelayCommand(ResetRun);
    }

    private PlotModel BuildChartModel()
    {
        var model = new PlotModel
        {
            PlotAreaBorderColor = OxyColor.FromRgb(60, 62, 78),
            TextColor = OxyColor.FromRgb(200, 202, 214),
            Background = OxyColor.FromRgb(0x2A, 0x2C, 0x36)
        };

        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = "Sample #",
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromRgb(50, 52, 64),
            TextColor = OxyColor.FromRgb(200, 202, 214),
            TitleColor = OxyColor.FromRgb(200, 202, 214)
        });
        model.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Value",
            MajorGridlineStyle = LineStyle.Solid,
            MajorGridlineColor = OxyColor.FromRgb(50, 52, 64),
            TextColor = OxyColor.FromRgb(200, 202, 214),
            TitleColor = OxyColor.FromRgb(200, 202, 214)
        });

        _upperA = ZoneLine(OxyColor.FromAColor(90, OxyColors.OrangeRed));
        _lowerA = ZoneLine(OxyColor.FromAColor(90, OxyColors.OrangeRed));
        _upperB = ZoneLine(OxyColor.FromAColor(70, OxyColors.Goldenrod));
        _lowerB = ZoneLine(OxyColor.FromAColor(70, OxyColors.Goldenrod));
        _clLine = ZoneLine(OxyColors.LightGray, 1.5, LineStyle.Dash);
        _uclLine = ZoneLine(OxyColors.OrangeRed, 1.5, LineStyle.Solid);
        _lclLine = ZoneLine(OxyColors.OrangeRed, 1.5, LineStyle.Solid);

        foreach (var line in new[] { _upperA, _lowerA, _upperB, _lowerB, _clLine, _uclLine, _lclLine })
            model.Annotations.Add(line);

        _valueSeries = new LineSeries
        {
            Color = OxyColor.FromRgb(0x4F, 0xC3, 0xF7),
            MarkerType = MarkerType.Circle,
            MarkerSize = 3,
            MarkerFill = OxyColor.FromRgb(0x4F, 0xC3, 0xF7),
            StrokeThickness = 1.5
        };
        model.Series.Add(_valueSeries);

        return model;
    }

    private static LineAnnotation ZoneLine(OxyColor color, double thickness = 1, LineStyle style = LineStyle.Dot) =>
        new()
        {
            Type = LineAnnotationType.Horizontal,
            Color = color,
            StrokeThickness = thickness,
            LineStyle = style,
            Y = 0
        };

    private void EstablishBaselineFromSimulator()
    {
        var baseline = _simulator.GenerateBaseline(30);
        var (_, _, unit) = _simulator.GetProfile();
        ApplyBaseline(baseline, $"30 simulated readings ({unit})");
    }

    private void EstablishBaselineFromCsv()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Import baseline readings"
        };
        if (dialog.ShowDialog() != true)
            return;

        List<double> values;
        try
        {
            values = CsvService.ImportValues(dialog.FileName);
        }
        catch (Exception ex)
        {
            StatusText = $"Could not read file: {ex.Message}";
            return;
        }

        const int minPoints = 15;
        if (values.Count < minPoints)
        {
            StatusText = $"Import failed — found only {values.Count} numeric values, need at least {minPoints} to establish a baseline.";
            return;
        }

        ApplyBaseline(values, $"{values.Count} imported readings from {Path.GetFileName(dialog.FileName)}");
    }

    /// Shared by both baseline sources: hands the sample to the analyzer, then pushes
    /// the resulting center line / control limits onto the chart's reference lines.
    private void ApplyBaseline(IReadOnlyList<double> baselineValues, string sourceDescription)
    {
        try
        {
            _analyzer.EstablishBaseline(baselineValues);
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
            return;
        }

        var limits = _analyzer.Limits;
        _clLine.Y = limits.CenterLine;
        _uclLine.Y = limits.UCL;
        _lclLine.Y = limits.LCL;
        _upperA.Y = limits.UpperZoneA;
        _lowerA.Y = limits.LowerZoneA;
        _upperB.Y = limits.UpperZoneB;
        _lowerB.Y = limits.LowerZoneB;

        BaselineEstablished = true;
        StatusText = $"Baseline set from {sourceDescription} — CL={limits.CenterLine:F2}, σ={limits.Sigma:F3}. Ready to run.";
        ChartModel.InvalidatePlot(true);
    }

    private void AddReading(double value)
    {
        var m = new Measurement { Sequence = _sequence++, Timestamp = DateTime.Now, Value = value };
        _history.Add(m);

        var violations = _analyzer.Evaluate(_history);
        _valueSeries.Points.Add(new DataPoint(m.Sequence, m.Value));

        foreach (var v in violations)
        {
            Application.Current.Dispatcher.Invoke(() => Violations.Insert(0, v));
        }
        if (violations.Count > 0)
            ViolationCountText = $"{Violations.Count} violation{(Violations.Count == 1 ? "" : "s")}";

        // Keep the visible window to the most recent 150 points so the chart stays readable.
        const int maxVisible = 150;
        if (_valueSeries.Points.Count > maxVisible)
            _valueSeries.Points.RemoveAt(0);

        ChartModel.InvalidatePlot(true);
    }

    private void ExportCsv()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"spc_run_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dialog.ShowDialog() == true)
        {
            CsvService.ExportMeasurements(dialog.FileName, _history);
            var violationsPath = dialog.FileName.Replace(".csv", "_violations.csv");
            CsvService.ExportViolations(violationsPath, Violations);
            StatusText = $"Exported {_history.Count} readings and {Violations.Count} violations.";
        }
    }

    private void ResetRun()
    {
        _timer.Stop();
        IsRunning = false;
        BaselineEstablished = false;
        _history.Clear();
        _sequence = 0;
        _valueSeries.Points.Clear();
        Violations.Clear();
        ViolationCountText = "0 violations";
        _simulator.ClearDrift();
        _analyzer.Reset();
        StatusText = "Establish a baseline to begin.";
        ChartModel.InvalidatePlot(true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
