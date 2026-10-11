# 1.9.1-pre.4 — address the independent pre.1 audit

- Restore legacy plain-HTTP search/lyrics as the last resort after the HTTPS interfaces fail or return unusable responses. As requested by the fork owner, fallback is enabled by default with an **Allow HTTP fallback (unencrypted metadata)** setting to disable it. Existing configurations default to enabled. Valid secure empty searches and declared instrumentals do not trigger HTTP fallback.
- Reserve 12 seconds of the 30-second total lookup deadline for lyric retrieval; search requests stop at 18 seconds. Pending interfaces share their phase's remaining time, capped at eight seconds per request. A new-interface timeout disables that interface for the remainder of the lookup; transport exceptions disable the failed interface. A fresh lookup starts with fresh availability state. This improves available-fallback access without extending the overall deadline or guaranteeing success for arbitrarily slow servers.
- Disable automatic redirects so an HTTPS response cannot silently choose plain HTTP outside the explicit fallback policy. API redirect responses are treated as unusable and logged by status code.
- Accept song URLs on exact host `y.music.163.com`, including `/m/song` phone shares, alongside `music.163.com`. Non-song routes and deceptive domains remain rejected.
- Treat NetEase's simplified/traditional Chinese instrumental placeholder as a valid absence, including timestamped/metadata-tagged text, without suppressing real lyrics that contain that phrase alongside other text. Explicit `nolyric` also suppresses nonempty placeholders.
- Send the fixed `appver` marker only on legacy requests. It identifies an application version; it is not an account/login cookie. Automatic server-cookie replay remains disabled from pre.3; no account cookies are imported.
- Include regressions for HTTPS refusal/stalling, HTTP opt-out persistence, reserved lyric time, mobile override callbacks, instrumental text, cookie/header scope and redirects. The three reported phone-link/time-budget/placeholder failures reproduce against pre.3 and pass with these changes.
- Assembly/file version: `1.9.1.3`; product version: `1.9.1-pre.4`.

The auditor's HTTPS-blocking explanation for upstream issue #20 remains a hypothesis. Simulated network failures validate fallback behavior; native TLS behavior inside MusicBee, VPN conditions and the actual issue reporter's network still require host testing. Ranking and parsing concerns from her pre.1 audit were already addressed in pre.2, and the shared-client cookie regression was addressed in pre.3. Full LRC timestamp normalization/metadata preservation remain outstanding.

# 1.9.1-pre.3 — keep anonymous searches independent of server cookies

- Disable the production HTTP handler's automatic cookie storage/replay. Anonymous searches retain the existing public request headers, without accumulating server-set session cookies. Controlled live requests found the correct recording initially but returned unrelated successful responses on subsequent requests with cookie handling enabled. Disabling it kept the same searches valid. No account cookies are required or imported.
- This cookie persistence was introduced by the pre.1 reliability update, which shared one HTTP client across a lookup. Upstream created a new client for each request. It is a regression in this fork's earlier update, not an established cause of the user's original upstream wrong-song selection.
- Keep the pre.2 title/artist/album/duration checks and fallback behavior. The matching safeguards reject unrelated responses; this transport fix prevents the observed session contamination that stopped later searches finding the correct song.
- Add a local HTTP regression fixture that sets a cookie on the first response and returns an unrelated song if that cookie is replayed. It fails against pre.2 and passes with the fix, exercising the actual production HTTP handler rather than a mocked handler.
- Verification: 76 regression cases pass in both x64/x86 processes. Live automatic selection returns 400875204 after a preliminary broad search; both matching modes through the fake MusicBee host callback return lyrics equal to that recording, with Custom10 absent. Native MusicBee testing remains the user checkpoint.
- Assembly/file version: `1.9.1.2`; product version: `1.9.1-pre.3`.

Correction to the pre.2 investigation: its exploratory live probe reused a session after earlier searches. Cookie state contaminated the subsequent results. The conclusion that recording 400875204 was absent from search was incorrect. Independent requests and cookie-disabled multi-request sessions return it; automatic selection and lyrics retrieval work from the supplied title/artist/album/duration without Custom10. The exact server-side interpretation of those cookies is not established.

# 1.9.1-pre.2 — conservative matching

- Reject search candidates unless the whole title and artist credit support the match. Case, whitespace, punctuation and Unicode compatibility variants normalize consistently; default matching permits very small spelling differences on sufficiently long names/titles. Short names require exact normalization.
- Keep the first tagged artist as primary. A secondary composer/guest credit alone is insufficient unless exact title, exact normalized album and close duration also corroborate it. Additional composer credits do not disqualify a correctly credited primary singer.
- Replace the squared duration penalty with a bounded contribution of at most five points. Reject substantially different durations and recording editions. Exact title/artist/album matches distinguish album variants; album differences otherwise remain soft evidence because translations and compilations are common.
- Return no match when plausible recordings remain too close to distinguish. Do not use song ID or server ordering as identity evidence.
- Search individual tagged artists before broad title-only searches, preserving credit order. If a successful search returns no eligible candidates, try the legacy search interface within the existing lookup deadline.
- Preserve real names such as AC/DC, Simon & Garfunkel, Tyler, The Creator and 迈克尔·杰克逊. Support explicit slash/semicolon/Japanese-comma artist separators. Parse actual `feat.`, `featuring` and `ft.` clauses without damaging words like "feather".
- Apply acceptance checks to the legacy mode too. Its saved fuzzy setting now permits small spelling differences; it no longer accepts an unchecked first result. The settings label reflects that behavior.
- Include 100 distinct public search candidates captured while reproducing the supplied "By Your Side" case as an offline fixture. Its correct recording (400875204) is accepted when present; unrelated returned candidates are rejected. The fixture contains metadata only, with no audio or lyrics. There is no song-specific production rule.
- Display the full build label in the plugin description. Assembly/file version: `1.9.1.1`; product version: `1.9.1-pre.2`.

The initial live probe reproduced the earlier matcher selecting unrelated songs from successful search answers. It incorrectly concluded the correct recording was absent from search; see the correction and transport fix under pre.3 above. Pre.2 still rejects the captured bad answers, and Custom10 `netease=400875204` remains an explicit override. This is not proof of an anti-bot mechanism or an explanation of upstream issue #20.

Stricter checks can reduce matches for translated/transliterated titles, incomplete credits or reordered credits without corroborating album/duration. Custom10 remains the explicit override. Full LRC timestamp normalization and metadata-preserving translation merging remain outstanding. Actual MusicBee testing of this increment is still required; the user confirmed pre.1 worked normally.

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

At the pre.1 checkpoint, ranking, artist parsing and match confidence remained unresolved; pre.2 addresses these above. Timestamp normalization and preservation of LRC metadata during translation merging remain unresolved.

Actual MusicBee installation, plugin loading, settings persistence, library scans and manual re-search still need host testing before publishing. A successful public NetEase request does not establish availability for every account, region or recording. The cause of upstream issue #20 remains unproven.
