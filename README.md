# Statistical-Process-Control-Monitor
A WPF desktop application that simulates process sensor data from a semiconductor
fab chamber temperature, chamber pressure, deposition rate, RF power and applies
**Statistical Process Control (SPC)** to detect when a process drifts out of control.
It implements the same core methodology (Individuals control charts + the Western
Electric rules) used on real production tools for in-line quality monitoring.

![Tech](https://img.shields.io/badge/.NET-8%2F10-512BD4) ![UI](https://img.shields.io/badge/UI-WPF-0078D7) ![Chart](https://img.shields.io/badge/Chart-OxyPlot-orange)

---

## Features

- **Individuals control chart (I-chart)** with a live center line, ±1σ/±2σ/±3σ zone
  bands, and control limits, rendered with OxyPlot
- **Two ways to establish a baseline:**
  - **Random** — generates 30 synthetic in-control readings and computes limits from them
  - **From CSV** — imports a real (or sample) dataset and computes limits from that instead
- **Baseline estimation** via the average moving range method (`σ = MRbar / 1.128`),
  the standard way to estimate process sigma for individual readings without subgrouping
- **Western Electric Rules** engine, checked on every new point:
  - Rule 1 — a single point beyond 3σ
  - Rule 2 — 2 of 3 consecutive points beyond 2σ on the same side
  - Rule 3 — 4 of 5 consecutive points beyond 1σ on the same side
  - Rule 4 — 8 consecutive points on the same side of the center line
- **Live simulator** with on-demand **spike** (single outlier → Rule 1) and **drift**
  (gradual mean shift over 20 ticks → Rules 2/3/4) injection, so violations can be
  demonstrated on demand instead of waiting for a real fault
- **Violation log** — timestamp, value, rule name, and a plain-English description
- **CSV export** of a full run (readings + violations) for reports or a demo artifact
  
---

## Project layout

```
Statistical-Process-Control-Monitor/
├── SpcMonitor.sln
├── README.md
└── SpcMonitor/
    ├── SpcMonitor.csproj        # net8.0-windows, WPF, OxyPlot.Wpf package reference
    ├── App.xaml / App.xaml.cs   # application entry point, loads the theme dictionary
    ├── MainWindow.xaml          # the entire UI — toolbar, chart, violation grid, status bar
    ├── MainWindow.xaml.cs       # code-behind; only wires DataContext to the ViewModel
    │
    ├── Models/                  # plain data, no UI or WPF dependency
    │   ├── Measurement.cs       # one reading: sequence, timestamp, value, sigma zone, violation flag
    │   ├── Violation.cs         # a flagged rule breach: which rule, description, reading it belongs to
    │   └── ControlLimits.cs     # center line, sigma, and the derived UCL/LCL/zone boundaries
    │
    ├── ViewModels/
    │   ├── MainViewModel.cs     # orchestrates simulator + analyzer + chart; exposes bindable
    │   │                        # properties and commands consumed by MainWindow.xaml
    │   └── RelayCommand.cs      # minimal ICommand implementation (no MVVM framework dependency)
    │
    ├── Services/                # business logic, independent of the UI
    │   ├── SpcAnalyzer.cs       # baseline calculation + the four Western Electric rules
    │   ├── DataSimulator.cs     # generates synthetic readings; supports spike/drift injection
    │   └── CsvService.cs        # CSV export (readings, violations) and CSV import (baseline)
    │
    └── Assets/
        └── Theme.xaml           # dark theme: colors, button/grid styles shared across the UI
```

**Layer responsibilities, briefly:**
- **Models** hold data only, no logic, no UI awareness.
- **Services** hold the actual SPC math and I/O, independent of WPF; each one could be
  unit-tested with plain xUnit/NUnit without touching the UI at all.
- **ViewModel** is the only place that talks to both the Services layer and the View,
  via `INotifyPropertyChanged` properties and `ICommand` bindings.
- **View** (`MainWindow.xaml`) contains no logic, every control binds to the
  ViewModel; the code-behind's only job is setting `DataContext`.

---

## Requirements

- Windows 10/11
- Visual Studio 2022 (17.8+) with the **.NET desktop development** workload, or the
  .NET 8 (or later — see note below) SDK + your editor of choice
- Internet access on first build, so NuGet can restore `OxyPlot.Wpf`

## Build & run

**Visual Studio**
1. Open `SpcMonitor.sln`.
2. Build once (`Ctrl+Shift+B`) to let NuGet restore `OxyPlot.Wpf`.
3. Press `F5` (or `Ctrl+F5` to run without debugging).

**Command line**
```bash
git clone https://github.com/sakiiif/Statistical-Process-Control-Monitor.git
cd Statistical-Process-Control-Monitor
dotnet restore
dotnet build
dotnet run --project SpcMonitor
```

## Using the app

1. **Pick a process parameter** from the dropdown (Chamber Temperature, Chamber
   Pressure, Deposition Rate, or RF Power). This selects the simulator's target
   mean/stddev.
2. **Establish a baseline**, either:
   - **Establish Baseline (Random)** — instantly samples 30 clean synthetic readings, or
   - **Establish Baseline (from CSV)** — opens a file picker; the file needs at least
     15 numeric values in its first column (a header row and extra columns are fine
     and are ignored).
   Either way, this computes the center line, sigma, and control limits, and draws
   them on the chart.
3. **Start** to begin streaming readings onto the chart in real time.
4. **Inject Spike** to force a single extreme outlier (tests Rule 1), or **Inject
   Drift** to start a gradual 3.5σ mean shift over the next 20 readings (typically
   trips Rules 3, then 4, then 2, roughly in that order as the drift progresses).
5. Watch the **Violations** panel. Each entry shows which rule fired and why.
6. **Export CSV** to save the full run (readings + a companion violations file).
   Useful for a demo screenshot/GIF or for regression-checking the rules engine
   against a known dataset.
7. **Reset** clears the current run so you can establish a fresh baseline and start over.

---
