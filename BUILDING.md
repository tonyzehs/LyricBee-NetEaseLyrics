# Build and test

Use Windows with .NET Framework 4.8 to run the plugin and tests, and the .NET SDK selected by `global.json` to build. The framework reference-assembly package restores automatically; Visual Studio and a developer pack are not required.

The plugin project is in `src/`, tests are in `tests/`, and the solution and build script are at the repository root. Develop on `dev`; `master` carries stable updates.

## Build the installation ZIP

From the repository root:

```powershell
.\build.ps1
```

If the SDK is not on PATH, or to choose the output folder:

```powershell
.\build.ps1 -DotNetPath 'C:\path\to\dotnet.exe' -OutputDirectory artifacts/dist
```

The script restores locked dependencies, builds Release configurations, and runs the regression executable in isolated x64 and x86 processes. Each process loads the AnyCPU plugin DLL with its embedded dependencies, without loose dependency DLLs. A failed build or test stops packaging.

`artifacts/dist/` contains the installation ZIP, `SHA256SUMS.txt`, a ZIP checksum file and test reports. The ZIP contains `mb_NeteaseLyrics.dll`, `LICENSE`, `LICENSE-Zony`, `NOTICE` and bilingual `INSTALL.md`. `SHA256SUMS.txt` lists the installer and its DLL. Published releases attach the installer and `SHA256SUMS.txt`; GitHub supplies Source code archives from the tag.

## Offline tests and settings previews

`build.ps1` runs the complete suite. The console runner exits nonzero on failure; `dotnet test` is not the entry point. Tests use a fake MusicBee host, injected responses, recorded metadata and local loopback fixture servers. Ordinary builds do not query GitHub or NetEase.

After building, focused checks and previews are available:

```powershell
.\tests\bin\Release\net48\NeteaseLyricsTest.exe --audit-update-tests
.\tests\bin\Release\net48\NeteaseLyricsTest.exe --settings-tests
.\tests\bin\Release\net48\NeteaseLyricsTest.exe --layout-tests
.\tests\bin\Release\net48\NeteaseLyricsTest.exe --scaled-settings-preview artifacts/previews
```

The preview command produces English/Chinese settings at simulated 100%, 150% and 200% font/viewport sizes. It does not change Windows DPI or reproduce MusicBee's skin engine. The offscreen runner keeps one STA message context alive across forms and enables cross-thread control checks.

For independent rollback probes, export the plugin's load-time settings repair:

```powershell
.\tests\bin\Release\net48\NeteaseLyricsTest.exe --repair-settings old.json repaired.json
```

Compile `tests/RollbackProbe.cs` as a standalone .NET Framework console executable, then run `RollbackProbe.exe plugin.dll repaired-settings.json http://127.0.0.1:PORT`. Use a separate invocation for each shipped DLL. The local fixture proxy must serve all NetEase HTTP/HTTPS responses.

## Optional live checks

These commands contact NetEase only when explicitly run. They print IDs, counts and status without saving fetched lyrics. `--live-recording` sends the public metadata in the By Your Side fixture; `--live-pronunciation` requests the known IDs in the pronunciation fixtures.

```powershell
.\tests\bin\Release\net48\NeteaseLyricsTest.exe --live
.\tests\bin\Release\net48\NeteaseLyricsTest.exe --live-recording
.\tests\bin\Release\net48\NeteaseLyricsTest.exe --live-pronunciation
```

CI runs the offline build script on Windows and uploads artifacts. It does not publish releases or run live NetEase checks. Checksums identify the delivered files; byte-identical rebuilds in different folders are not established.

Use the fresh MusicBee checklist in [docs/RELEASING.md](docs/RELEASING.md) for each release. Runtime rules are documented in [UPSTREAM-CHANGES.md](UPSTREAM-CHANGES.md), and version history is in [CHANGELOG.md](CHANGELOG.md).
