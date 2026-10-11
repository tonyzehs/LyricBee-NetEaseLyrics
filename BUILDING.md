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

Offline tests inject an HTTP handler, exercising request construction, encrypted form generation, response parsing, fallback, cancellation and the MusicBee callback with a fake host. Local loopback HTTP fixtures also exercise the real production handler: response cookies must not contaminate later requests, and redirects must not silently bypass the fallback policy. Simulated HTTPS refusal/stalling verifies recovery over HTTP and the setting that disables it. They do not emulate MusicBee's native plugin loader or NetEase's private server behavior. Tests are a console runner that exits nonzero on failures; `dotnet test` is not the test entry point.

An optional live smoke test makes a public search and known-ID lyric requests, including the legacy HTTPS endpoint. It prints counts/status only and does not save fetched lyrics:

```powershell
.\tests\bin\Release\net48\NeteaseLyricsTest.exe --live
```

An additional optional `--live-recording` check sends the public metadata in the embedded "By Your Side" fixture to NetEase. It compares automatic selection and lyrics with the known recording after an earlier broad search, then checks both matching modes through the MusicBee callback with Custom10 absent. It prints IDs/status only; downloaded lyrics are not saved.

Before a release, install the ZIP in MusicBee, check all three lyric formats and both matching modes, change/reopen settings, perform a background lookup for a different track while another song plays, and retry lyrics with desktop and phone-share Custom10 song URLs. Test missing tags, a network outage and a slow/HTTPS-blocked connection; verify HTTP opt-out persists and prevents the insecure fallback. Confirm the plugin description says `Build 1.9.1-pre.4` and keep a copy of the previously installed plugin for rollback. MusicBee's numeric version may still display 1.9.1. Native TLS must be tested inside MusicBee, since a test executable can inherit different runtime/network defaults.

As requested by the fork owner, plain-HTTP fallback is enabled by default, including configurations from earlier builds. It sends search metadata without encryption when HTTPS interfaces fail or return unusable responses. Turn off **Allow HTTP fallback (unencrypted metadata)** to require HTTPS. No account cookies are used, automatic server-cookie storage is disabled, and redirects are not followed automatically. The fixed `appver` marker identifies an application version and is sent only to legacy interfaces.

Lookup is capped at 30 seconds; searches stop at 18 seconds so lyric retrieval has at least 12 seconds remaining. Pending fallback interfaces share the remaining time in their phase, with each request capped at eight seconds. A timed-out new interface is skipped for the rest of that lookup and becomes eligible again in a fresh lookup. This bounds waits and preserves a fallback opportunity; it cannot guarantee successful lyrics for arbitrarily slow servers.

Automatic matching returns no lyrics for contradictory or ambiguous candidates. Legacy fuzzy matching allows minor spelling differences, while retaining the acceptance checks. If a wrong lyric was previously saved/cached, clear that lyric in MusicBee before judging a fresh search: returning no new lyric may leave an existing lyric visible. The supplied "By Your Side" recording now works in live automatic tests without Custom10; the previous negative investigation was contaminated by server-set cookies. Custom10 `netease=400875204` remains an optional explicit override on the requested file.

CI runs the same offline build script on Windows and uploads test artifacts. It does not publish releases or run live NetEase requests.

Dependencies are locked and clean source builds have been verified. Byte-for-byte reproducibility is not established: builds in different folders produced different module identifiers/checksums after dependency embedding, despite the compiler's deterministic setting. Published checksums identify the delivered artifacts.
