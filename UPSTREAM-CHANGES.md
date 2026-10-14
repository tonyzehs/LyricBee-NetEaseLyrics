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

## Current runtime rules

### Matching and compatible listings

Contradictory or ambiguous candidates return no new lyrics. The spelling checkbox permits only minor differences; number, artist and recording checks still apply. Track-prefix fallback requires the file's matching TrackNo, known durations within two seconds, full artist agreement and an exact normalized album when supplied. Plain-space prefixes also require zero padding: `01 Lemon` can normalize, while `7 Rings`, `99 Problems` and `1 2 Step` keep their numbers. Ordinary queries run first; at most one cleaned-title query follows when no eligible recording is found. Differing valid lowercase Roman tokens remain protected pairwise without treating title-case words such as Mc as numbers.

Equivalent-listing recovery tries at most three IDs when original lyrics are missing. When only extras are missing, it tries at most one more listing. Skipping translation retries for known Mandarin labels is opportunistic; it does not obtain a language label just for that shortcut. Custom10 remains final. Ordinary soundtrack attribution removal requires artist, album, duration and named-work corroboration.

The 1.9.8 testing build adds a fallback after ordinary searches find no eligible candidate. It requires an exact base title, identical complete artist sets and known durations within two seconds. Only a terminal service-side attribution with an explicit film/series/game medium is removable, and the service album must be a soundtrack containing the named work. Exact album aliases/translations from the response can corroborate the local album. Otherwise, a CJK soundtrack tag containing a meaningful CJK name and no other Latin work name may match a Latin attributed work. This does not establish that the two work names are translations; a sole, otherwise corroborated recording is accepted despite that uncertainty. An unrelated CJK soundtrack with the same title, performer and near-identical duration cannot always be distinguished without additional catalogue evidence.

The fallback does not resolve ordinary ambiguity, combine with artist-alias/TrackNo relaxations, accept incomplete artist sets, or ignore live/remix/demo/piano labels. Different attributed service works block the fallback even if their scores differ. Ordinary same-script mismatches retain their existing rules. Existing request budgets, duplicate grouping and Custom10 behavior are retained; no additional network requests or bundled mappings are introduced. Diagnostic reports identify the localized fallback without recording raw metadata.

### Requests and fallback

A lookup has a 30-second limit, with search ending at 18 seconds to reserve at least 12 seconds for lyrics. Actual sends are capped at nine search requests and nine lyric-phase requests, including fallback and optional language metadata. Multi-artist searches can exhaust that budget before every query is tried. Pending interfaces share the remaining phase time; each request is capped at eight seconds. A timed-out interface is skipped for that lookup and can be used again on a new lookup.

Plain HTTP fallback is enabled by default and can be disabled in settings. It sends unencrypted search metadata only after a legacy HTTPS transport failure or temporary server error. Secure HTTP/API refusals from search or lyrics, including anti-bot responses, block insecure fallback for the lookup; malformed, incomplete or empty secure replies alone do not authorize it. Optional language/artist metadata refusals do not change this policy. Other secure interfaces may still be tried. No account cookies are used, server cookies are not retained, and redirects are not followed automatically. The fixed `appver` marker is sent only to legacy interfaces.

Optional language metadata uses HTTPS only, with a two-second request limit inside the lookup budget. Its refusal does not change lyric transport fallback. Known labels use a bounded six-hour per-ID cache. Unknown or multilingual labels retain translation behavior; missing metadata permits conservative kana/Hangul inference. Lyrics-only overrides may request a language label even without pronunciation.

### Verified artist aliases (1.9.7)

When ordinary matching finds no eligible song, candidates with an exact title and album and a known duration within two seconds may verify the primary artist through NetEase's `/api/artist/{id}` metadata. Only exact normalized names from that artist record can supply the missing identity. A secondary composer/guest alias cannot identify the singer, and additional local artist credits still need direct support. Candidate grouping retains the original service artist credits.

Artist metadata is HTTPS only and shares the nine-request search cap and search deadline, with at most two two-second requests per lookup. More than two plausible primary artist IDs are left unmatched. Refusals, redirects, malformed replies and mismatched artist IDs cannot authorize a match. Artist refusal does not block otherwise permitted lyric HTTP fallback, and does not authorize an artist request over HTTP. Original lyric download time is reserved separately.

The process-wide memory cache keeps at most 256 artist IDs, each with at most 33 bounded names (display name plus aliases). Successful records expire after 24 hours; definite failures such as refusals, malformed records or a wrong artist ID cool down for five minutes. Timeouts, cancellations, transport failures and temporary server errors are not negative-cached, so a new lookup can retry immediately. Injected test transports use independent caches. No catalogue names are bundled and no cache is written to disk. Alias resolution currently requires the full title and album to agree; it does not combine alias verification with otherwise uncorroborated soundtrack or track-prefix removal.

### Settings, rollback and display

Original lyrics are always included. Fresh settings use translation on, pronunciation off, separate lines and Follow default for every language. Unsaved changes, including interface switches, remain a draft until Save. Settings rows grow for wrapped text and large fonts; a small host can scroll.

Two-line output is stored with the legacy-compatible Format=1 and TwoLineTranslation=true. Old Format=3 settings migrate with a `.pre9.bak` backup. Load-time repair also clears unsafe legacy matching flags, retaining the old file as `netease_config.pre-spelling.bak`. Keep the repaired settings during rollback; historical backups can restore unsafe flags. Resolve a reported migration/write failure before rollback. Older versions can discard newer preferences when saving, so reselect them after returning to LyricBee. Upstream v1.8.1 still has its own weaker matching behavior, including accepting a same-title cover in the rollback fixture.

MusicBee can retain an existing wrong lyric when a new lookup returns nothing. Clear stored lyrics before judging a fresh search. Extra text is aligned independently within the firm 50 ms limit; missing or unusable tracks fall back to the remaining extra, then original only. Recorded fixtures retain public metadata/timing but replace lyric text with synthetic labels; the soundtrack fixture contains no audio or lyrics.

### Diagnostic reports and update checks

Copy diagnostic report is available under collapsed Advanced in both stable and development builds. It keeps five lookups in memory, the first twenty and last twenty structured events per lookup, and at most 32 KiB in the report. Lookup ages are relative to copying; event elapsed milliseconds remain, and a gap line counts omitted events. Closing the plugin clears the recorder.

Reports include outcome/interface codes, request counts, selection/grouping/normalization decisions, NetEase IDs, build/runtime and whitelisted display settings. They exclude lyrics, tag values, paths, URLs, cookies, response bodies and exception messages; arbitrary trace text is not recorded. Copying is manual and sends nothing to a server. Song IDs can reveal listening history, as disclosed beside the button. Unsupported and ambiguous matching are distinguished, but not every rejected candidate is enumerated.

Manual update checks use a separate anonymous HTTPS client, with an eight-second timeout and 256 KiB reply limit. They send no song metadata and never run automatically on startup or opening settings. Results and rate-limit messages stay inline; opening a release requires the separate View update click. No package is downloaded or installed automatically. Closing settings or changing interface language cancels a pending check. Redirects remain disabled and lyric HTTP preferences do not apply to GitHub.

Recognized informational versions (`X.Y.Z-test.N`, `X.Y.Z-pre`, `X.Y.Z-pre.N`, `X.Y.Z-preN`) show DEV BUILD in the description; stable versions do not. Branch names and SDK version overrides do not determine that label. Failed update checks log a reason category, without response or exception text.
