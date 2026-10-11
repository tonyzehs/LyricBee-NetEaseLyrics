# Build and test

Use Windows with .NET Framework 4.8 installed to run the plugin/tests and the .NET SDK selected by `global.json` to build. The framework reference-assembly package restores automatically; Visual Studio or a developer pack is not required.

From the repository root, run:

```powershell
.\build.ps1
```

The script restores locked dependencies, builds Release configurations, and runs the regression executable in both 64-bit (AnyCPU on 64-bit Windows) and 32-bit processes. Both runs load the same AnyCPU plugin DLL that goes into the package. Each run uses an isolated folder containing only the test executable/config and the plugin DLL, checking that embedded dependencies load without loose dependency DLLs. It then produces a MusicBee ZIP containing only `mb_NeteaseLyrics.dll`, a SHA-256 checksum and test reports in `artifacts/dist`. A failed build or regression test stops packaging.

If the SDK executable is not on PATH:

```powershell
.\build.ps1 -DotNetPath 'C:\path\to\dotnet.exe'
```

Offline tests inject an HTTP handler, exercising request construction, encrypted form generation, response parsing, fallback, cancellation and the MusicBee callback with a fake host. They do not emulate MusicBee's native plugin loader or NetEase's private server behavior. Tests are a console runner that exits nonzero on failures; `dotnet test` is not the test entry point.

An optional live smoke test makes a public search and known-ID lyric requests, including the legacy HTTPS endpoint. It prints counts/status only and does not save fetched lyrics:

```powershell
.\tests\bin\Release\net48\NeteaseLyricsTest.exe --live
```

Before a release, install the ZIP in MusicBee, check all three lyric formats and both matching modes, change/reopen settings, perform a background lookup for a different track while another song plays, and retry lyrics with a Custom10 song URL. Test missing tags and a network outage. Confirm the loaded plugin version is 1.9.1 and keep a copy of the previously installed plugin for rollback.

CI runs the same offline build script on Windows and uploads test artifacts. It does not publish releases or run live NetEase requests.

Dependencies are locked and clean source builds have been verified. Byte-for-byte reproducibility is not established: builds in different folders produced different module identifiers/checksums after dependency embedding, despite the compiler's deterministic setting. Published checksums identify the delivered artifacts.
