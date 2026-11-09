# FitnessTimer

[中文 README](README.md)

A lightweight Windows desktop interval training timer: WPF / C# / .NET 8, single window, no browser or localhost workflow, all data stored as local JSON.

## Features

- **Plan editor**: multiple exercise groups, each with its own training and rest durations; add / delete / move up / down, saved automatically
- **Timer flow**: preparation countdown → training → rest loops, with pause / resume / stop / skip-phase
- **Compact mode**: minimal always-on-top window showing countdown, action, phase, and group count; remembers the window mode you last closed in
- **History**: every session is logged (duration, completed groups, skips)
- **Statistics**: week / month bar charts, drawn in pure WPF — no charting dependencies
- **Dark mode**, window position memory

## Download

Grab the zip from [Releases](https://github.com/daze0515/FitnessTimer/releases), unzip, and run `FitnessTimer.App.exe`. Requires the .NET 8 Desktop Runtime ([download](https://dotnet.microsoft.com/download/dotnet/8.0)).

## Build from source

Requires the .NET 8 SDK on Windows.

```bat
scripts\run-app.cmd    :: build and run
scripts\test.cmd       :: run all unit tests
```

Or directly: `dotnet build FitnessTimer.sln` / `dotnet test FitnessTimer.sln`.

## Layout

```
FitnessTimer.App/       WPF app (Models / Services / ViewModels / Views)
FitnessTimer.Tests/     MSTest unit tests
scripts/                Build / test scripts
```

Runtime data (training plans, history, settings) lives in a `data/` folder next to the app and is created with defaults on first launch.

## License

[MIT](LICENSE)
