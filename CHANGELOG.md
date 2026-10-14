# 1.9.7-test.2 — artist-alias review follow-ups (local testing build)

- Keep optional artist metadata refusals separate from the lyric HTTP-fallback policy, matching optional language metadata. Artist requests remain HTTPS only; search/lyric refusals still block insecure fallback.
- Allow immediate retry after artist metadata timeouts, cancellations, network failures or temporary server errors. Keep the five-minute cooldown for definite refusals or invalid records.
- Print the full artist response size in bytes during `--live-alias`, without logging response content. Use the trace: “Matching: matched through a verified NetEase artist alias”.
- Add regression checks for optional refusal, HTTP opt-out, transient-failure retry, UTF-8 reply measurement and trace wording. Record the 奇縁ロマンス example as checked in MusicBee and add it to the per-release checklist.
- Product version: `1.9.7-test.2`; assembly version: `1.9.7.0`; file version: `1.9.7.2`. Local testing only; publication requires approval.

# 1.9.7-test.1 — verified artist aliases (local testing build)

- Match localized NetEase artist names against verified aliases from the same artist ID when the title and album agree exactly and durations differ by at most two seconds. Keep the primary singer and additional artist-credit checks.
- Fix automatic lookup of **奇縁ロマンス** tagged **ナナヲアカリ** when NetEase lists **七音阿卡莉**. Add recorded search/artist metadata to offline regression tests and the English/Chinese README examples.
- Cache up to 256 artist records in memory for 24 hours; failed/empty records have a five-minute cooldown. Limit alias retrieval to two HTTPS requests of at most two seconds each, within the existing nine-request search cap and search deadline. Ordinary matches and Custom10 overrides make no alias requests.
- Include structured alias lookup/cache/match events in diagnostic reports without artist names, tags, paths or response bodies. No aliases are bundled and no new runtime dependencies are added.
- Product version: `1.9.7-test.1`; assembly version: `1.9.7.0`; file version: `1.9.7.1`. Local testing only; publication requires approval.

# 1.9.6

- Add **Check for LyricBee updates** in settings, with inline results and a separate **View update** button. Checks are manual; the plugin does not download or install updates.
- Show English and Chinese GitHub rate-limit messages, contain unexpected button errors, and log failed checks without response or exception text. Share version rules and allow latest-release replies up to 256 KiB.
- Use relative lookup times in diagnostic reports, retaining the first twenty and last twenty events with a count of omitted events.
- Let settings rows resize for wrapped text and larger fonts, keeping the existing controls, order and defaults.
- Include the post-1.9.5 source rearrangement into `src/` and corrected `DataStructures.cs` filename. Lyric matching and display rules are unchanged from 1.9.5.
- Product version: `1.9.6`; assembly/file version: `1.9.6.0`.

# 1.9.6-test.4 — automatic settings layout (local testing build)

- Replace fixed control positions with automatically sized table and flow rows. Preserve controls, defaults, order, headings, bilingual labels, collapsed Advanced and the manual update flow.
- Grow wrapped hints/status/privacy text without overlapping following controls. Allow scrolling in small/high-scale viewports and retain full selected dropdown text. Bold headings follow host font changes.
- Isolate the layout refactor from test.3's update-check/diagnostic fixes. Product version: `1.9.6-test.4`; assembly/file version: `1.9.6.4`. Native MusicBee skin/DPI testing is still needed; publication is not approved.

# 1.9.6-test.3 — update-check and diagnostic follow-ups (local testing build)

- Log one categorized update-check failure without response or exception text. Add the agreed English/Chinese GitHub rate-limit messages and a last-resort guard around the button handler.
- Share version rules between development labels and update comparisons; raise the response limit to 256 KiB. Redirect and lyric matching policies are unchanged.
- Apply the approved installed-version, available-version and no-newer-stable-release wording in both languages.
- Replace absolute lookup timestamps with report-relative ages. Retain the first twenty and last twenty events, with a count of skipped middle events; keep elapsed event milliseconds and existing privacy/size bounds.
- Product version: `1.9.6-test.3`; assembly/file version: `1.9.6.3`. Fixed settings positions are retained in this build so the automatic-layout change can be tested separately. Local only, not release approval.

# 1.9.6-test.2 — clarify update-button scope (local testing build)

- Label the check button Check for LyricBee updates / 检查 LyricBee 更新 so it cannot be confused with an update check for MusicBee or other extensions.
- Label the release-page button View update / 查看更新. Identify LyricBee in the installed-version, checking, available-version and failure messages; keep results inline and browser opening a separate choice.
- Update bilingual documentation. Product version: `1.9.6-test.2`; assembly/file version: `1.9.6.2`. Network, version-comparison and lyric behaviour are unchanged. Local testing only.

# 1.9.6-test.1 — inline update checking (local testing build)

- Add a manual Check for updates button in English and Simplified Chinese settings. Checking, current-version and failure messages stay inside the panel, without popups.
- Reveal Update found only when a newer stable release exists. Opening the release page requires a separate click; checking never opens the browser, downloads or installs a package.
- Check the public GitHub latest-release endpoint over HTTPS with a bounded response and timeout, without song metadata, cookies or credentials. Compare numeric versions and treat a same-number stable release as newer than its test build. Cancel pending checks when settings close or the interface language changes.
- Keep lyric matching and display rules unchanged. Product version: `1.9.6-test.1`; assembly/file version: `1.9.6.1`. Local testing only; GitHub pushes and publication await a later approved batch.

# Post-1.9.5 maintenance

- Move the plugin project, implementation, assembly metadata, locked dependencies and Fody configuration into `src/`. Keep the root solution/build entry point and tests folder; update project references and package paths.
- Correct the source filename `DataStucture.cs` to `DataStructures.cs`. Plugin filename/provider/settings identities and runtime behaviour are retained. The published v1.9.5 tag and assets remain unchanged.

# 1.9.5

- Repair saved matching settings automatically for safer rollback from 1.9.4. Unsaved settings and interface-language edits no longer affect live lookups or disk until Save.
- Improve matching for contractions, dotted abbreviations, remaster and soundtrack notes; protect numbered titles and only remove strongly corroborated track-number prefixes.
- Keep usable translation and pronunciation when an original line has a malformed timestamp; preserve the position of malformed text after repeated cues and discard blank continuations.
- Recover from compatible listings with missing or instrumental lyrics, while limiting retries when only extra text is missing.
- Recover from temporary server errors with the configured HTTP fallback; keep anti-bot refusals and unsafe redirects blocked.
- Add Copy diagnostic report under a collapsed Advanced section in English and Simplified Chinese settings. Reports stay local until manually copied and exclude lyrics, tag values, paths and account data.
- Refresh bilingual documentation and screenshots. Product version: `1.9.5`; assembly/file version: `1.9.5.0`.

# 1.9.5-test.4 — diagnostic reports in all builds (local testing build)

- Include Copy diagnostic report in stable and development builds, under a collapsed Advanced section in the English and Simplified Chinese settings. Reports remain bounded, memory-only and manually copied; no upload is made. The song-ID privacy note stays beside the button.
- Remove the former development-only compilation flag, packaging/version guards and separate CI profiles. This supersedes the test.3 guard policy following Tony's decision to offer diagnostics to all users. Development markers now depend on the actual declared version; stable builds retain the button without a DEV BUILD label.
- Explain where to find the report in both READMEs and shorten the DLL-size improvement wording. These README changes will reach master with the approved 1.9.5 release.
- Product version: `1.9.5-test.4`; assembly/file version: `1.9.5.4`. Local testing only; publication still requires Tony's approval. Previous frozen builds and audit evidence are retained.

# 1.9.5-test.3 build safeguard (superseded by test.4)

- Reject diagnostics-enabled builds with stable, missing or unrecognized version labels in the project itself, covering direct SDK builds as well as the packaging script. Check the DLL's declared informational version rather than overridable SDK version properties.
- Add project-guard checks for stable builds, both architecture/configuration paths, recognized development labels, invalid labels and SDK version overrides. Runtime code and the audited test.3 installer are unchanged.

# 1.9.5-test.3 — matching safeguards and development diagnostics (local testing build)

- Keep malformed text after the earliest occurrence of a compressed repeat-tag line; discard whitespace-only continuations after malformed rows. Valid pronunciation and translation remain aligned independently.
- Reject differing valid lowercase Roman-numeral tokens, such as Symphony vii/viii, without classifying title-case names as numerals or relaxing the existing spelling threshold.
- Tighten TrackNo-prefix matching: plain-space prefixes require zero padding, and all stripped matches require known durations within two seconds, full artist agreement and an exact normalized album when present. 7 Rings, 99 Problems and 1 2 Step retain their numbers even when the original title is absent from search results.
- Run ordinary searches first, then at most one cleaned-title query if they produce no eligible recording. Existing request/time limits remain unchanged.
- Finish aligning rollback instructions: keep repaired current settings; historical backups may restore unsafe flags.
- Add an explicitly enabled development-only Copy diagnostic report button in English and Simplified Chinese, with DEV BUILD labels and disclosure that NetEase song IDs can reveal listening history. The bounded in-memory report excludes lyrics, tags, paths, URLs, cookies and response/exception text; copying sends nothing to a server.
- Compile the recorder, clipboard hook and diagnostic controls out of the default profile. Refuse diagnostic packaging for stable informational versions. CI on dev checks both profiles.
- Product version: `1.9.5-test.3`; assembly/file version: `1.9.5.3`. Development work is on `dev`; `master` remains unchanged. This build is unpublished and needs independent audit and real MusicBee checks before release.
- All 354 diagnostic-profile and 350 non-diagnostic-profile checks pass in each isolated x64/x86 process, with no build warnings/errors. Stable diagnostic packaging is rejected before SDK invocation; the non-diagnostic DLL contains no diagnostic recorder or controls.

# 1.9.5-test.2 — isolate malformed cues (local testing build)

- Retain a malformed original line as untimed text after its preceding valid cue, including its untimed continuations. Valid cues still receive independently aligned pronunciation/translation; broken original or auxiliary stamps never supply alignment times. Repeated tags do not duplicate an isolated line. Entirely unusable original clocks or unpaired extras retain the untouched original.
- Give consistent rollback advice in both READMEs, INSTALL, settings documentation and BUILDING: keep the repaired current settings; historical backups can restore unsafe flags.
- Restrict non-contextual Roman-numeral classification to uppercase tokens, with case-only equivalent titles still accepted and decimal-number boundaries preserved. Explicit lowercase part labels normalize as numbers. Mixed-case words no longer introduce numeral conflicts; short-title similarity limits remain unchanged (the seven-letter Mix It Up / Mixx It Up example still fails the existing .9 threshold).
- Support a plain-space leading track number when it agrees with the file's TrackNo tag. Treat removal as a fallback: supported unmodified titles such as 7 Rings retain priority. Existing title, artist, edition, ambiguity and request-count safeguards remain in force.
- Include the packaged DLL hash in SHA256SUMS.txt as well as the installer hash. The source checksum is added when preparing the source package.
- Missing-extra retries under fresh defaults are capped at two compatible listings. The Mandarin shortcut is opportunistic only when a service label is already available; it makes no additional metadata requests and is not counted as a default saving.
- Product version: `1.9.5-test.2`; assembly/file version: `1.9.5.2`. No release has been published. Real MusicBee verification remains required.
- All 343 checks pass in isolated x64 and x86 processes with no build warnings/errors. Replaying the auditor's unchanged offline probes against this DLL confirms retained valid translations around malformed stamps, plain-space track prefixes, two lyric attempts for missing extras and Custom10's final instrumental response.

# 1.9.5-test.1 — audit follow-up (local testing build)

- Store spelling allowance separately from legacy matching flags. Repair existing settings on load without requiring Save; write Fuzzy=false and UseLegacyMatch=false for older readers, preserve the effective current choice and HTTP opt-out, and keep a byte-preserving pre-spelling backup. Failed repairs retain the loaded preferences and original file.
- Edit preferences in a separate draft. Switching interface language retains unsaved controls without affecting live lookups or disk; Save applies the draft, and closing the panel without saving discards it.
- Limit missing-extra recovery to one additional compatible listing. Missing originals still allow three listings. Reuse already available Mandarin language metadata to avoid unnecessary translation-only retries, without requesting language metadata just for this purpose or guessing from Han text.
- Continue past an instrumental listing during equivalent-listing recovery. Explicit Custom10 IDs and single listings remain final.
- Treat HTTP/API 5xx responses as temporary interface failures. Retain HTTP opt-out, refusal blocking for 4xx/anti-bot answers, disabled automatic redirects and existing request/time caps.
- Normalize contractions and dotted abbreviations before numeral checks, retain meaningful standalone Roman numerals, and recognize additional remaster-note spellings. Strip a leading track-number prefix only when it agrees with the requested file's TrackNo tag.
- Permit soundtrack album aliases when both albums contain the attributed work; retain full artist agreement, known durations within two seconds, exact base title and meaningful version/conflicting-work guards. Different credits and wider-duration duplicate groups remain rejected.
- Preserve malformed original timing in its original order by falling back to the unchanged original, rather than moving lyric text into the header. Uninstall removes plugin-owned backups and GUID temporary files.
- Replace short real-time deadline fixtures with controlled cancellation tokens. Add permanent audit counterexamples and an optional standalone runner for published rollback DLLs. Older v1.8.1 matching remains inherently weaker: disabling its unchecked fuzzy mode cannot prevent every same-title cover mismatch.
- Product version: `1.9.5-test.1`; assembly/file version: `1.9.5.1`. No GitHub release or tag has been published.
- All 331 checks pass in isolated x64 and x86 processes against the packaged DLL. Published-DLL rollback probes cover v1.8.1, v1.9.0, pre.7 and 1.9.3; see BUILDING.md for the remaining upstream v1.8.1 matching limitation.

# 1.9.4 — bilingual settings and language overrides

- Always include original lyrics. Separate the Translation and Pronunciation checkboxes from the common layout setting; use spaced ` / ` separators in same-line output and retain two-/three-line timed cues.
- Add English/Simplified Chinese settings with Automatic UI-language selection and a bilingual language-selector label. Switching interface language preserves unsaved preferences.
- Group language controls under Language overrides. Fresh installations use Follow default for every language. Japanese and Korean also offer the four original-inclusive content choices; Cantonese appears below them and offers Lyrics or Lyrics + Pronunciation; English offers Lyrics or Lyrics + Translation.
- Identify English from service metadata without guessing from Latin text. Retain conservative script fallback and existing request/time limits.
- Replace spelling customization and its nested hint/control with one Allow small spelling differences checkbox, preserving older effective preferences and recording-identity guards.
- Migrate Translation-only to original + translation, retain original-only behavior, preserve existing language/network preferences, and back up older settings before the first save. Keep valid legacy format values for rollback; older builds cannot represent every new override combination.
- Package bilingual installation instructions with the DLL, licenses and attribution. Preserve the existing plugin/provider identity for normal upgrades.
- All 313 regression checks pass in isolated x64 and x86 processes. The local settings iterations were 1.9.4-test.1 and 1.9.4-test.2.
- Assembly/file version: `1.9.4.3`; product version: `1.9.4`.

# 1.9.3 — LyricBee standalone project

- Continue development on master in `tonyzehs/LyricBee-NetEaseLyrics`, with the full upstream/fork history and historical release tags preserved.
- Use **LyricBee — NetEase Lyrics for MusicBee** as the project title and **LyricBee — NetEase Lyrics** in MusicBee. Credit Tony Y as maintainer and Charlie Jiang as the original author; retain the existing copyright and licenses.
- Update download/documentation links and include LICENSE, LICENSE-Zony and NOTICE in the installation package.
- Keep the existing `mb_NeteaseLyrics.dll` filename, provider identity and saved configuration keys so upgrading replaces the previous plugin and preserves preferences.
- Matching, lyric formatting, language defaults and network behavior are unchanged from 1.9.2. Historical release packages retain their original filenames and hashes.
- Assembly/file version: `1.9.3.0`; product version: `1.9.3`.

# 1.9.2 — explicit translation defaults

- Make the existing default preferences explicit: Japanese and Korean use Lyrics + Translation; Cantonese pronunciation is off. The four choices for each language remain available. Existing saved preferences are retained; this update does not reset selected pronunciation modes.
- Retain pre.12's matching, formatting, language controls and readability changes. Default translation uses separate lines. Re-fetch lyrics after changing preferences.
- Assembly/file version: `1.9.2.0`; product version: `1.9.2`. Published as the latest of seven milestones. Source packages contain the documentation from each build; tags identify those source snapshots.

# 1.9.1-pre.12 — four language choices and readable settings

- Add four Japanese and Korean choices: Lyrics; Lyrics + Translation; Lyrics + Pronunciation; Lyrics + Pronunciation + Translation. Each language is independent. Lyrics returns the unchanged original and stops retries once a usable original exists. The global Lyrics only and Translation only choices retain precedence.
- Preserve the numeric meaning of existing language settings; add Lyrics as value 3 and map UI order explicitly. Pre.10/pre.11 safely fall back to translation for this new value, and saving settings there can discard it. Back up settings before rollback. Defaults remain Lyrics + Translation.
- Classify original-only language choices even without a pronunciation track, using the existing bounded HTTPS language request and cache. Failed metadata uses conservative kana/Hangul inference; unresolved, unsupported or multilingual labels retain general translation behavior. The two-second metadata limit, one metadata request per lookup, request caps and deadlines remain unchanged.
- Replace the inactive spelling checkbox with a normal readable hint while customization is off. Show the enabled checkbox when customization is on and preserve its saved choice when toggling. MusicBee's native disabled checkbox rendering caused the dark text seen on a dark skin.
- Shorten the host description to NetEase lyrics plus the build number; shorten global format labels, keep JP / CN spacing, and widen dropdown lists as needed for their measured contents.
- Add 14 regression checks covering both languages/all four choices/both combined layouts, stable save/reopen values, global precedence, original metadata preservation, missing pronunciation, bounded metadata failure, caching, retry counts and spelling-hint visibility. Re-fetch stored lyrics after changing preferences. Automated checks do not cover MusicBee skin rendering.
- Assembly/file version: `1.9.1.11`; product version: `1.9.1-pre.12`.

# 1.9.1-pre.11 — guarded soundtrack attribution matching

- Fix the Stromae/Pomme **Ma Meilleure Ennemie** lookup: NetEase's matching recording appends `(from the series Arcane League of Legends)` while the local title omits it. Recognize terminal English film/series/game/anime/musical attributions in parentheses, brackets or a spaced dash suffix. Bare `from X` also requires soundtrack/OST/original-score album context.
- Remove only a corroborated attribution: complete local artist credits and album must agree exactly, durations must be known and within two seconds, and the named work must occur in the album. Compare the remaining title exactly after existing normalization; retain title numbers and other edition labels. Do not remove arbitrary parentheses, substantive title words or uncorroborated attributions.
- Reject conflicting named works, including near-spelling variants. Attributed titles without enough corroboration require full canonical title equality rather than fuzzy spelling. Conflicting attribution sources cannot merge as equivalent listings through a plain-title duplicate; keep existing pairwise grouping and ambiguity rejection.
- Compare recording editions after removing a supported attribution so a work named **Live Forever** does not make a studio recording look like a live performance. Other live, remix, edit, cover, instrumental and substantive version labels remain significant. Trace the rule without raw song metadata. No search requests, lyric requests, timeout limits, settings or pronunciation behavior are added or changed.
- Add 17 regression checks with recorded public search metadata, reversed candidate order, both matching modes, full-credit/album/duration guards, source conflicts, existing numbered titles and meaningful variants. All 232 checks pass in x64 and x86 against the packaged AnyCPU DLL.
- Live HTTPS checks through the lyric callback in a simulated MusicBee host select ID 3374505659 with unchanged tags and no Custom10, returning the correct original and bilingual lyrics. Automatic lookup was also verified in MusicBee. Re-fetch stored lyrics after upgrading.
- Assembly/file version: `1.9.1.10`; product version: `1.9.1-pre.11`.

# 1.9.1-pre.10 — pronunciation preferences and three-line output

- Read NetEase's `romalrc` pronunciation alongside original and translated lyrics. Explicitly request it on the legacy endpoint using `rv=-1`; the existing encrypted request already requests it. Do not combine the newer word-timed `yromalrc` clock with ordinary LRC.
- Add **Cantonese: enable pronunciation** and independent **Japanese** / **Korean** dropdowns: Translation, Pronunciation, Pronunciation & Translation. Defaults preserve translation behavior, with Cantonese pronunciation off. Apply only to combined formats; original-only and translation-only keep priority. Keep the secondary same-line option's **JP / CN** label and use generic extra-text labels to cover pronunciation.
- Separate-line combined output uses one timestamp for original, pronunciation, then translation. Align each extra independently under the existing exact/50 ms, one-use, offset and ambiguity rules. Omit missing, unaligned or duplicate lines; keep the usable extra, then the unchanged original if both fail. Pronunciation-only does not silently substitute translation. Keep spaced slash pairs for untimed equal-count lines and adjacent verse blocks for compatible unequal counts. Never invent timing.
- Choose the language using NetEase's song-language metadata. An optional HTTPS-only request runs only when pronunciation is present and a non-default language preference is active. Cap it at two seconds and one request per lookup, sharing the existing nine lyric-phase requests and 30-second total deadline. Default translation settings make no optional request. Optional metadata refusals do not suppress normal lyric transport fallback.
- Cache known language labels by song ID for six hours, at most 512 entries; do not cache lyric text or failed/unknown labels. Missing metadata allows conservative kana/Hangul inference for Japanese/Korean. Never infer Cantonese from Han-only lyrics; unsupported or explicitly multilingual metadata retains translation behavior. Do not generate pronunciation locally.
- Compatible-listing recovery seeks both requested extras using each listing's own original timing. Retain the best partial result if alternatives fail; preserve Custom10's single-ID behavior and existing request caps. Malformed new preference types reset only those fields and retain old valid settings, including HTTP opt-out. Persist new preferences separately from rollback-safe Format and TwoLineTranslation fields. Earlier builds can discard new preferences when saving settings.
- Add 44 regression checks, including sanitized captured Cantonese/Japanese/Korean timing replays, per-cue fallback, missing tracks, duplicate text, language ambiguity, stalled metadata, independent preferences, cache isolation and compatible-listing recovery. All 215 checks pass in x64 and x86 against the packaged AnyCPU DLL. The optional `--live-pronunciation` mode prints counts/status only. Three-line scrolling and highlighting still require verification in MusicBee; these tests check the returned lyric text.
- Re-fetch stored lyrics after changing these settings. Assembly/file version: `1.9.1.9`; product version: `1.9.1-pre.10`.

# 1.9.1-pre.9 — translation alignment and compatibility

- Include all pre.8 number, duplicate, annotation, alternate-listing and request-limit changes. Replace the separate same-line parser with the shared numeric parser used by two-line output. Preserve metadata, offsets and simultaneous cues; normalize one-digit seconds and up to nine fractional digits to milliseconds by truncating extra precision. Invalid timing lines are isolated from neighboring cues; recognized untimed credits move to the header instead of interrupting a bilingual pair.
- Match exact timestamps first. Remaining translations use their unique nearest original within a firm 50 ms maximum, once each and in order. Equal-distance candidates and near duplicates of exact matches are skipped. Keep approximate pairs only when pairing covers a strict majority of the smaller usable cue sequence. Compare raw and offset-adjusted clocks by pair count, summed error and maximum error. Equal-quality conflicting maps retain common exact pairs only. Preserve original playback offsets and trace alignment decisions without logging lyric text. Sorted-time neighbor searches avoid quadratic matching.
- Preserve untimed section markers without counting them as lyric lines. Use spaced ` / ` for supported line pairs; unequal but corresponding verses retain original lines followed by translation lines, without collapsing paragraphs or adding language-specific labels. Unmatched verse structures still preserve the original. Both bilingual output modes use this behavior. Timed two-line output remains the default.
- Treat metadata-only, credit-only and slash-only text as absent lyrics/translations. Translation-only mode falls back to a usable original when the translation has no lyric text. Compatible-listing recovery can continue past metadata-only translations. Blank translations no longer add a slash in same-line output.
- Persist two-line format using `Format=1` and `TwoLineTranslation=true`. Read existing choices and migrate old `Format=3` files with a `.pre9.bak` backup; migration failures retain loaded settings, including HTTP opt-out. Older binaries see a valid same-line format. An older version saving settings can remove the separate flag.
- Permit plain HTTP only after the legacy HTTPS interface has a transport failure, including a timeout, with the opt-out still honored. Secure HTTP/API refusals block HTTP for the rest of that lookup; malformed or empty secure payloads alone do not authorize downgrade. Secure-interface fallback still handles missing lyrics. Timed-out interfaces are skipped for the rest of the lookup. Time and request caps remain unchanged.
- Add 36 formatting, alignment, performance, migration and transport-policy regressions, alongside pre.8's 24 matching checks. All 171 checks pass in x64 and x86 processes against the packaged AnyCPU DLL. These automated checks use fixtures and controlled network conditions; native rendering and live network behavior require separate verification.
- Assembly/file version: `1.9.1.8`; product version: `1.9.1-pre.9`.

# 1.9.1-pre.8 — numbered titles and catalogue duplicates

- Prevent title numbers from being accepted as spelling errors, including changed, missing and added numbers. Normalize explicit `Pt. II` / `Part 2` labels with conventional uppercase Roman numerals. Protect standalone uppercase Roman numerals without rewriting ordinary words into numbers. Remove only recognized remaster annotations before comparing numeric identity.
- Group different IDs only when normalized titles, full service artist credits and version labels agree, all service durations are known, and every pair is within two seconds. Prefer album similarity, then local duration difference, then ID. Retain ambiguity rejection between different groups and unknown-duration listings. No lyric comparisons are made during grouping; this is a compatibility heuristic, not proof of identical recordings.
- Treat recognized remaster annotations as notes. Mono/stereo and album-version annotations are removable only when local and service durations are known and within two seconds. Live, remix, radio-edit, instrumental and original-mix distinctions remain significant. Custom10 still selects only the requested ID.
- Try at most three compatible listings when lyrics or requested translations are absent. Keep the first usable original if alternatives fail; use an alternative's original and translation together, never mixing their clocks. Stop on explicit instrumental responses. Original-only mode does not retry for translations.
- Cap each lookup at nine search and nine lyric requests, as well as the existing 18-second search and 30-second total deadlines. Several-artist lookups can therefore stop before all possible queries run. Grouping decisions, recognized annotations and alternate lyric requests emit short trace messages using IDs rather than raw tags or paths.
- Rename the misleading legacy option to **Customize spelling matching**, preserving saved fields and spelling choices. Actual MusicBee callback lookups in either mode retain album and duration metadata.
- Add 24 regression checks with multiple cases, including the recorded Jonas Blue "By Your Side" duplicates and both numeric counterexamples, pairwise duration bounds, annotation guards, fallback preservation and request limits. Keep the Japanese ID 400875204 and wrong-artist/title/edition checks. All 135 checks pass in x64 and x86 processes.
- Re-fetch old lyrics after upgrading: these changes do not replace lyrics MusicBee already stored. Native MusicBee checks are separate from the fixture tests. Pre.9 adds alignment fixes, rollback-compatible format storage and a stricter HTTP fallback policy.
- Assembly/file version: `1.9.1.7`; product version: `1.9.1-pre.8`.

# 1.9.1-pre.7 — spaced separators for untimed lyrics

- Keep timed original/translation output on two lines, without a generated slash, and retain that default. Change only the untimed fallback of this format to language-neutral `original / translation` pairs.
- Replace the separate Original:/Translation: sections. Pair nonblank lines in provider order when counts agree, preserving original metadata and blank verse breaks. With differing line counts but equal blank-separated verse counts, pair verses and combine split/merged lines within each verse. When neither structure agrees, preserve the original instead of forcing pairings. Positional correspondence is a best effort formatting assumption, not semantic verification.
- Strip translation timestamps when originals are untimed; never borrow or invent timing. Blank or metadata-only translations preserve originals. Literal slashes remain intact. The optional same-line timed formatter and saved settings are unchanged.
- Add five regressions for spaced line/verse pairs, unpairable structures, metadata/literal slashes and blank translations; update existing untimed expectations. All 111 cases pass in x64 and x86 processes.
- Assembly/file version: `1.9.1.6`; product version: `1.9.1-pre.7`.

# 1.9.1-pre.6 — two-line output by default, without slash markers

- Remove the generated `/` marker from two-line output. Each pair still uses one timestamp on the original followed by the translation on the next line. Literal slashes in lyric text are preserved; the optional same-line format retains its separator.
- Default new/missing configurations and invalid-format recovery to **Original text and translation (two lines)**. Retain explicit saved original-only, translation-only, same-line and two-line choices when upgrading, along with legacy no-translation preferences.
- Two-line scrolling and highlighting were verified in MusicBee's Now Playing panel. The cue structure is unchanged; this update removes the slash marker and makes two-line output the default.
- Untimed behavior remains labelled original/translation sections in two-line mode. Historically, upstream v1.9.0's combined formatter could return empty text for wholly untimed originals when a translation was present; pre.1–pre.4 preserved only the original through an empty-merge fallback. Original-only and translation-only modes passed through their respective text, with original fallback when translation was absent.
- Update formatting expectations and add configuration compatibility coverage. All 106 regression cases pass in x64 and x86 processes.
- Assembly/file version: `1.9.1.5`; product version: `1.9.1-pre.6`.

# 1.9.1-pre.5 — optional stacked bilingual lyrics

- Add **Original text and translation (two lines)** as a fourth output choice. Existing enum values and saved settings remain compatible; the same-line output remains the default. The selection persists through MusicBee's Apply/Save flow.
- Emit one timestamp for each bilingual cue: original text followed by `/`, then an untimestamped translation continuation. The single timestamp groups both lines into one cue. Fixture tests check the returned text; Now Playing scrolling and highlighting were verified with pre.5.
- In the new format, align equivalent numeric timestamps, including one/two/three decimal places and colon fractions; sort numerically; expand repeated timestamps; group simultaneous lines into one cue; retain original metadata and existing text continuations. Apply each document's offset when finding matching translations, preserving the original offset for host playback. Blank or missing translations do not add `/`.
- Use labelled original/translation sections when original lyrics are untimed. Strip translation time tags in that case rather than borrowing their timing. Do not infer pairs from equal line counts. A timed original with untimed translation is retained without fabricated pairing.
- Preserve the existing original-only, translation-only and same-line formatters. General metadata/timing fixes in the existing same-line formatter remain outstanding.
- Automatic lookup for "By Your Side" (ID 400875204) was verified in MusicBee with pre.3. Network fallback and phone-link behavior still require separate checks in MusicBee.
- Add 15 regressions for host callback formatting, settings persistence, normalized timestamps, offsets, repeated/simultaneous cues, literal slashes, newline conventions and untimed fallbacks. All 105 cases pass in x64 and x86 processes.
- Assembly/file version: `1.9.1.4`; product version: `1.9.1-pre.5`.

# 1.9.1-pre.4 — network recovery and phone-share links

- Restore legacy plain-HTTP search/lyrics as the last resort after the HTTPS interfaces fail or return unusable responses. Fallback is enabled by default with an **Allow HTTP fallback (unencrypted metadata)** setting to disable it. Existing configurations default to enabled. Valid secure empty searches and declared instrumentals do not trigger HTTP fallback.
- Reserve 12 seconds of the 30-second total lookup deadline for lyric retrieval; search requests stop at 18 seconds. Pending interfaces share their phase's remaining time, capped at eight seconds per request. A new-interface timeout disables that interface for the remainder of the lookup; transport exceptions disable the failed interface. A fresh lookup starts with fresh availability state. This improves available-fallback access without extending the overall deadline or guaranteeing success for arbitrarily slow servers.
- Disable automatic redirects so an HTTPS response cannot silently choose plain HTTP outside the explicit fallback policy. API redirect responses are treated as unusable and logged by status code.
- Accept song URLs on exact host `y.music.163.com`, including `/m/song` phone shares, alongside `music.163.com`. Non-song routes and deceptive domains remain rejected.
- Treat NetEase's simplified/traditional Chinese instrumental placeholder as a valid absence, including timestamped/metadata-tagged text, without suppressing real lyrics that contain that phrase alongside other text. Explicit `nolyric` also suppresses nonempty placeholders.
- Send the fixed `appver` marker only on legacy requests. It identifies an application version; it is not an account/login cookie. Automatic server-cookie replay remains disabled from pre.3; no account cookies are imported.
- Include regressions for HTTPS refusal/stalling, HTTP opt-out persistence, reserved lyric time, mobile override callbacks, instrumental text, cookie/header scope and redirects. Regression tests reproduce the phone-link, time-budget and instrumental-placeholder failures in pre.3 and pass with these changes.
- Assembly/file version: `1.9.1.3`; product version: `1.9.1-pre.4`.

The cause of upstream issue #20 remains unconfirmed. Simulated HTTPS failures verify fallback behavior, but do not establish the cause on affected installations. Pre.2 fixes ranking and parsing; pre.3 fixes cookie replay. LRC timestamp normalization and metadata preservation during translation merging are addressed in later builds.

# 1.9.1-pre.3 — keep anonymous searches independent of server cookies

- Disable the production HTTP handler's automatic cookie storage/replay. Anonymous searches retain the existing public request headers, without accumulating server-set session cookies. Controlled live requests found the correct recording initially but returned unrelated successful responses on subsequent requests with cookie handling enabled. Disabling it kept the same searches valid. No account cookies are required or imported.
- This cookie persistence was introduced by the pre.1 reliability update, which shared one HTTP client across a lookup. Upstream created a new client for each request. Cookie replay is a regression introduced in this fork; its connection to earlier upstream wrong-song reports remains unconfirmed.
- Keep the pre.2 title/artist/album/duration checks and fallback behavior. The matching safeguards reject unrelated responses; this transport fix prevents the observed session contamination that stopped later searches finding the correct song.
- Add a local HTTP regression fixture that sets a cookie on the first response and returns an unrelated song if that cookie is replayed. It fails against pre.2 and passes with the fix, exercising the actual production HTTP handler rather than a mocked handler.
- Verification: 76 regression cases pass in both x64/x86 processes. Live automatic selection returns 400875204 after a preliminary broad search; both matching modes through the fake MusicBee host callback return lyrics equal to that recording, with Custom10 absent. Automatic lookup was later verified in MusicBee.
- Assembly/file version: `1.9.1.2`; product version: `1.9.1-pre.3`.

Correction to the pre.2 investigation: its exploratory live probe reused a session after earlier searches. Cookie state contaminated the subsequent results. The conclusion that recording 400875204 was absent from search was incorrect. Independent requests and cookie-disabled multi-request sessions return it; automatic selection and lyrics retrieval work with the local title/artist/album/duration without Custom10. The exact server-side interpretation of those cookies is not established.

# 1.9.1-pre.2 — conservative matching

- Reject search candidates unless the whole title and artist credit support the match. Case, whitespace, punctuation and Unicode compatibility variants normalize consistently; default matching permits very small spelling differences on sufficiently long names/titles. Short names require exact normalization.
- Keep the first tagged artist as primary. A secondary composer/guest credit alone is insufficient unless exact title, exact normalized album and close duration also corroborate it. Additional composer credits do not disqualify a correctly credited primary singer.
- Replace the squared duration penalty with a bounded contribution of at most five points. Reject substantially different durations and recording editions. Exact title/artist/album matches distinguish album variants; album differences otherwise remain soft evidence because translations and compilations are common.
- Return no match when plausible recordings remain too close to distinguish. Do not use song ID or server ordering as identity evidence.
- Search individual tagged artists before broad title-only searches, preserving credit order. If a successful search returns no eligible candidates, try the legacy search interface within the existing lookup deadline.
- Preserve real names such as AC/DC, Simon & Garfunkel, Tyler, The Creator and 迈克尔·杰克逊. Support explicit slash/semicolon/Japanese-comma artist separators. Parse actual `feat.`, `featuring` and `ft.` clauses without damaging words like "feather".
- Apply acceptance checks to the legacy mode too. Its saved fuzzy setting now permits small spelling differences; it no longer accepts an unchecked first result. The settings label reflects that behavior.
- Include 100 distinct public search candidates captured for the offline "By Your Side" regression fixture. Its correct recording (400875204) is accepted when present; unrelated returned candidates are rejected. The fixture contains metadata only, with no audio or lyrics. There is no song-specific production rule.
- Display the full build label in the plugin description. Assembly/file version: `1.9.1.1`; product version: `1.9.1-pre.2`.

The initial live probe reproduced the earlier matcher selecting unrelated songs from successful search answers. It incorrectly concluded the correct recording was absent from search; see the correction and transport fix under pre.3 above. Pre.2 still rejects the captured bad answers, and Custom10 `netease=400875204` remains an explicit override. This is not proof of an anti-bot mechanism or an explanation of upstream issue #20.

Stricter checks can reduce matches for translated/transliterated titles, incomplete credits or reordered credits without corroborating album/duration. Custom10 remains the explicit override. Full LRC timestamp normalization and metadata-preserving translation merging remain outstanding. Basic retrieval with pre.1 was verified in MusicBee. The pre.2 matching changes were covered by fixtures; automatic lookup for ID 400875204 was verified in MusicBee after pre.3.

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

Pre.1 leaves ranking, artist parsing, match confidence and LRC processing unchanged. Pre.2 addresses matching; pre.5 and pre.9 address timestamp normalization and metadata preservation.

Automated tests do not cover MusicBee's native plugin loader, settings panel or library scans. A successful public NetEase request does not establish availability for every account, region or recording. The cause of upstream issue #20 remains unproven.
