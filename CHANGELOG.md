# 1.9.1-pre.1 — reliability increment

- Return no lyrics safely when both NetEase interfaces fail, refuse access or return malformed data. Validate search payloads and skip invalid candidates.
- Try the legacy interface when the new lyric interface returns success without usable original lyrics. Explicit `nolyric: true` responses are treated as instrumental tracks and do not trigger fallback.
- Use the title, artist and album supplied by MusicBee when file tags are unavailable. Skip searches without a title unless a valid song override exists.
- Read Custom10 from the requested file only. Accept positive `netease=ID` overrides and genuine NetEase song URLs; reject album, playlist, artist and unrelated-domain URLs.
- Parse fractional-second durations safely. Treat missing, zero or invalid duration as unknown and omit its ranking penalty. Handle missing candidate album/artist fields.
- Use an eight-second cancellation deadline per request and a thirty-second deadline shared by the lookup's search and lyric fallbacks. Reuse a client and cache identical search queries within a lookup. DNS/native networking can affect observed wall-clock cancellation time.
- Send concise failure diagnostics to MusicBee's trace callback in Release builds. Logs identify the endpoint stage and error/status/code, without response bodies or track metadata.
- Validate saved configuration, fix the initial and toggled state of the legacy-only fuzzy option, and guard saving when no live settings panel exists. Replace settings atomically; remove the old translation-preference marker only after a successful save.
- Preserve untimed original lyrics when combining translation would produce empty output. Treat a wholly blank translation as absent.
- Replace the incomplete package migration with an SDK project, pinned SDK and dependency lock files. Add offline regression tests, isolated-DLL checks, a build/package script and Windows CI. The plugin embeds runtime dependencies only; Harmony is absent.

The assembly/file version is `1.9.1.0`; the product version is `1.9.1-pre.1`. MusicBee only displays numeric versions.

## Remaining work

This increment does not fix the known over-weighted duration penalty when duration is present, unsafe featured-artist parsing, ambiguous artist delimiters, lack of a match-confidence threshold, timestamp normalization or preservation of LRC metadata during translation merging. These need separate matching and lyric-processing changes with broader fixtures.

Actual MusicBee installation, plugin loading, settings persistence, library scans and manual re-search still need host testing before publishing. A successful public NetEase request does not establish availability for every account, region or recording. The cause of upstream issue #20 remains unproven.
