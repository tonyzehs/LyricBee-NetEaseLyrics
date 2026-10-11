# Build and test

Use Windows with .NET Framework 4.8 installed to run the plugin/tests and the .NET SDK selected by `global.json` to build. The framework reference-assembly package restores automatically; Visual Studio or a developer pack is not required.

From the repository root, run:

```powershell
.\build.ps1
```

The script restores locked dependencies, builds Release configurations, and runs the regression executable in both 64-bit (AnyCPU on 64-bit Windows) and 32-bit processes. Both runs load the same AnyCPU plugin DLL that goes into the package. Each run uses an isolated folder containing only the test executable/config and the plugin DLL, checking that embedded dependencies load without loose dependency DLLs. It then produces a MusicBee ZIP containing `mb_NeteaseLyrics.dll`, `LICENSE`, `LICENSE-Zony` and `NOTICE`, a SHA-256 checksum and test reports in `artifacts/dist`. A failed build or regression test stops packaging.

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

Before a release, install the ZIP in MusicBee, check all four lyric formats and spelling customization, change/reopen settings, perform a background lookup for a different track while another song plays, and retry lyrics with desktop and phone-share Custom10 song URLs. Confirm the plugin description says `NetEase lyrics. Build 1.9.3`; The plugin name should be `LyricBee — NetEase Lyrics`. Native TLS must be tested inside MusicBee, since a test executable can inherit different runtime/network defaults. Record the MusicBee version, expected NetEase ID, observed lyric identity, translation behavior and outcome for each case below.

MusicBee verification checklist (the automated suite does not cover these checks):

- [ ] Duplicate-listed songs, including Jonas Blue "By Your Side" with absent/nonmatching album and Lia "鳥の詩" if its listings remain available. Confirm grouping traces and sensible selection.
- [ ] A Part 1 / Part 2 pair; the wrong part must not supply lyrics when the right one is unavailable.
- [ ] A remastered title and a meaningful live/remix or edit distinction; relax only the recognized note.
- [x] Automatic lookup for "By Your Side" (ID 400875204) without Custom10; correct lyrics verified in MusicBee.
- [x] Automatic lookup for Stromae/Pomme "Ma Meilleure Ennemie" (ID 3374505659) without Custom10; correct lyrics verified in MusicBee.
- [x] Two-line original/translation scrolling and highlighting in MusicBee's Now Playing panel.
- [ ] One song each with timed, untimed and offset lyrics; confirm scrolling and translation alignment. Include near stamps, unequal untimed line counts and raw versus adjusted offsets.
- [ ] Missing tags, network outage, slow/blocked HTTPS, and HTTP opt-out. Confirm bounded waits, fallback and no HTTP when disabled. Do not change system-wide networking for automated tests.
- [ ] Settings persistence, requested-file Custom10 ownership and one rollback to the previously installed plugin.

Clear incorrect stored lyrics and re-fetch when evaluating fixes; replaying a cached lyric does not test the new search. Back up the previous DLL and `netease_config` from MusicBee's persistent storage before upgrading. Pre.9 saves two-line output as Format=1 plus TwoLineTranslation=true, so earlier binaries see same-line output without a settings crash. Loading old Format=3 files migrates them with a .pre9.bak backup. If the trace reports migration failure, resolve the file write/backup issue before rollback, or restore/reset compatible settings manually. Earlier versions saving settings may discard the new flag; choose two-line output again when returning to 1.9.3.

Plain-HTTP fallback is enabled by default, including configurations from earlier builds. It sends search metadata without encryption only following a legacy HTTPS transport failure, including a timeout. A secure HTTP/API refusal blocks insecure fallback for that lookup. Malformed, incomplete or empty secure responses alone do not authorize HTTP; the other secure interface may still be tried. Turn off **Allow HTTP fallback (unencrypted metadata)** to require HTTPS. No account cookies are used, automatic server-cookie storage is disabled, and redirects are not followed automatically. The fixed `appver` marker identifies an application version and is sent only to legacy interfaces.

Lookup is capped at 30 seconds and eighteen network requests: at most nine search requests and nine lyric-phase requests, including all fallback interfaces and optional language metadata. Searches stop at 18 seconds so lyric retrieval has at least 12 seconds remaining. Compatible-listing recovery attempts at most three IDs. Multi-artist searches may reach the request cap before trying every possible query. Pending fallback interfaces share the remaining time in their phase, with each request capped at eight seconds. A timed-out interface is skipped for the rest of that lookup and becomes eligible again in a fresh lookup. This bounds waits and preserves a fallback opportunity; it cannot guarantee successful lyrics for arbitrarily slow servers.

Automatic matching returns no lyrics for contradictory or ambiguous candidates. Spelling customization can disable minor spelling differences, while retaining number, artist and recording checks and all available callback metadata. If a wrong lyric was previously saved/cached, clear that lyric in MusicBee before judging a fresh search: returning no new lyric may leave an existing lyric visible. Live automatic checks retrieve "By Your Side" (ID 400875204) without Custom10. The cookie-replay regression and its correction are documented in the changelog. Custom10 `netease=400875204` remains an optional explicit override on the requested file.

CI runs the same offline build script on Windows and uploads test artifacts. It does not publish releases or run live NetEase requests.

Dependencies are locked and clean source builds have been verified. Byte-for-byte reproducibility is not established: builds in different folders produced different module identifiers/checksums after dependency embedding, despite the compiler's deterministic setting. Published checksums identify the delivered artifacts.

Pronunciation tests cover independent language preferences, original/translation-only precedence, missing tracks, partial cues, offsets, the inclusive 50 ms alignment limit, duplicate auxiliary text, untimed verse formatting, language ambiguity, malformed metadata, a stalled metadata request, per-ID caching, compatible-listing recovery and settings compatibility. `tests/Fixtures/PronunciationRecordings.json` preserves actual timing, blank cues and language metadata from Cantonese ID 409931770, Japanese ID 1409311773 and Korean ID 2676664123, replacing all lyric text with synthetic labels. No downloaded full lyric text is distributed in that fixture.

An optional `--live-pronunciation` mode makes known-ID lyric and language requests, runs the packaged formatter, and prints only language, auxiliary-track and cue counts. It checks those three recordings and ID 400875204 for translation fallback with missing pronunciation. It does not save lyrics. Service changes can make a previously available sample fail; examine the trace rather than treating that as proof of a formatter bug.

Pronunciation checks in MusicBee:

- [ ] Cantonese checkbox on/off for ID 409931770; confirm both lines highlight and scroll.
- [ ] Japanese dropdown in all four positions; confirm three lines highlight and scroll as one cue.
- [ ] Korean choice independent of Japanese; confirm an English-only cue is not needlessly repeated.
- [ ] Japanese ID 400875204 with Lyrics + Pronunciation + Translation: missing pronunciation must retain the working original/translation display.
- [ ] Missing or unaligned extras: retain the usable extra, then original only when neither works.
- [ ] Save/reopen settings and rollback with the new fields present. Earlier builds may discard new preferences when saving; back up the configuration.

Optional metadata never downgrades to HTTP and a refusal there does not affect lyric transport fallback. Its request is capped at two seconds and remains inside the total request/time budgets; known language labels use a bounded six-hour per-ID cache. Unknown or multilingual language labels retain translation behavior, while missing language metadata permits conservative kana/Hangul inference. New language settings default to the previous translation behavior.

`tests/SoundtrackTests.cs` replays recorded public search metadata from `tests/Fixtures/MaMeilleureEnnemie.json`. The fixture contains no audio or lyrics. Its correct ID is 3374505659, matched against title Ma Meilleure Ennemie, artist Stromae/Pomme, the Arcane Season 2 soundtrack album and duration 147973 ms. This fixture checks attribution matching without changing request caps.

Automatic lookup for this recording has been verified in MusicBee. Additional host checks should include missing duration or album tags and a meaningful live/remix title. The trace should show attribution removal only when artist, album, duration and named work corroborate it. Otherwise, attributed titles require full-title equality.

`tests/ContentChoiceTests.cs` checks both languages in all four modes, both combined layouts, persistence with stable legacy values, malformed/unset choice fallback, global precedence, untouched original metadata/timing, absent pronunciation, bounded/stalled metadata, per-ID caching, compatible-listing stop counts and default spelling-hint visibility. The optional language request may now run without pronunciation when either language selects Lyrics. All request/time caps remain unchanged.

Settings checks in MusicBee:

- [ ] Dark and light MusicBee skins: the default spelling hint is readable; enabling customization shows an enabled checkbox and retains its previous choice.
- [ ] Description displays the build number without clipping; all four Japanese/Korean entries fit when selected and in the expanded list.
- [ ] Select each content mode, re-fetch a Japanese and a Korean song, and confirm original-only versus two-/three-line output. Check missing extras and independent preferences.
- [ ] Save/reopen existing translation/pronunciation/triple-line choices and new Lyrics choices. Back up config, load it with pre.11 and return to pre.12: old saved values retain meaning; new value 3 falls back to translation in pre.11. Saving there may discard the new choice.

Standalone WinForms previews verify layout and visibility only; they do not reproduce MusicBee's skin engine. MusicBee settings and scrolling checks are separate from these previews.

For 1.9.3, verify a fresh settings folder and an older config without language fields both show Japanese/Korean Lyrics + Translation and Cantonese pronunciation unchecked. A previously saved pronunciation choice remains selected. Preview screenshots do not establish how a particular MusicBee skin renders the settings.
