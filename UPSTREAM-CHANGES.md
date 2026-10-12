# Full changes over upstream v1.9.0

LyricBee continues [Charlie Jiang’s original plugin](https://github.com/cqjjjzr/MusicBee-NeteaseLyrics). This page keeps the detailed comparison previously listed in the README. See [CHANGELOG.md](CHANGELOG.md) for changes by version and the [README](README.md#fixes-over-upstream-v190) for the short overview.

### Bug fixes

- Handle refused, malformed, incomplete and missing network answers without crashing; try the legacy lyric interface when a successful newer response contains no usable lyrics.
- Handle missing tags and fractional durations safely, and use MusicBee's callback metadata when file tags are unavailable. An unknown duration no longer favors the shortest candidate.
- Replace the squared duration penalty and unchecked first-result selection with bounded scoring, identity checks and ambiguity rejection, reducing wrong-song pairing.
- Preserve real artist names such as AC/DC, Simon & Garfunkel and Tyler, The Creator; parse actual featured-artist clauses without damaging words such as “feather” or “featuring.”
- Prevent background lookups from borrowing Custom10 from the currently playing song; validate overrides and support mobile song links.
- Normalize and sort lyric times numerically, retain metadata/offsets, handle repeated and simultaneous cues, and align each extra once within the firm 50 ms limit. Compare raw and adjusted clocks conservatively.
- Preserve untimed originals, omit blank/slash-only auxiliary placeholders, and avoid trailing separators. Treat explicit instrumental responses and NetEase's pure-music placeholder as no lyrics.
- Validate saved settings, isolate malformed newer preference fields, save atomically and preserve valid choices when migration cannot be written. Store the two-line preference in an older-compatible format.

### Maintenance and other improvements

- Bound each lookup to 30 seconds, reserving lyric-fetching time after search. Cap actual sends at nine search and nine lyric-phase requests, including fallback and optional language metadata; stop reusing timed-out interfaces within that lookup.
- Reuse requests without replaying anonymous server cookies, disable automatic redirects, and provide an explicit HTTP fallback opt-out. The cookie replay problem introduced in this fork's pre.1/pre.2 builds was corrected in pre.3.
- Emit concise MusicBee trace diagnostics in Release builds for failures, matching rules and compatible-listing retries, without logging downloaded lyric bodies or local file paths.
- Use an SDK-style project, pinned SDK, locked dependencies, an isolated build/package script and Windows CI. Remove unused Harmony and embed only required runtime dependencies, reducing the DLL from roughly 4 MB upstream to roughly 1.1 MB.
- Add permanent recorded-metadata and synthetic lyric fixtures, both process architectures, real loopback transport probes, settings compatibility checks and reproducible packaging checksums. Byte-identical rebuilds in different folders are not claimed.
- Shorten clipped settings descriptions, retain saved preferences, simplify spelling matching to one checkbox, and separate default extras, common layout and language overrides. Add English/Simplified Chinese settings and a rollback backup for the new display preferences.
