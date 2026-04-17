# FlightDataRecorder

Desktop WPF (.NET 10) application for collecting MSFS telemetry via SimConnect, scoring landings, and exporting CSV datasets for ML workflows.

## What it does

- Connects to Microsoft Flight Simulator through SimConnect (x64).
- Records full telemetry (`_FULL.csv`) and ML-ready frame data (`_ML.csv`).
- Produces one-row landing feature summaries (`_LANDINGS_FEATURES.csv`).
- Automatically stops recording after rollout when speed drops below 40 kts.

## Tech stack

- C#
- .NET 10
- WPF
- Microsoft.FlightSimulator.SimConnect

## Output files

By default, CSV files are written to the user's Documents folder:

- `Lot_<Aircraft>_<timestamp>_FULL.csv`
- `Lot_<Aircraft>_<timestamp>_ML.csv`
- `Lot_<Aircraft>_<timestamp>_LANDINGS_FEATURES.csv`
