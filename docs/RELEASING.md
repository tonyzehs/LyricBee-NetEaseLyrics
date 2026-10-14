# Preparing release notes

Use [.github/RELEASE_TEMPLATE.md](../.github/RELEASE_TEMPLATE.md) for 1.9.5 and later. Keep the changes to three to six bullets about what users will notice. Release notes are English; installation instructions stay bilingual. Put hashes, source identity, regression counts and CI status under the collapsed Build details section.

Fill every placeholder from the actual release candidate and its verification records. Link the download to the installation ZIP, INSTALL.md to the release tag, and the full changes to the previous stable tag. Use the check count from the final packaged DLL; stable and development builds both include the diagnostic report under Advanced.

Preparing notes or a local build does not approve publication. Present the candidate, checksums and final notes to Tony before creating or publishing a release, uploading assets or pushing a release tag.

## Update-check response budget

The latest-release response must fit within 256 KiB, including metadata, release notes and asset records. Keep release notes concise and link longer documentation rather than embedding it. The checker keeps redirects disabled and makes no automatic download or installation.

## MusicBee checks for each release

Copy this list into a new release record and start with every box unticked. Record the candidate version/DLL hash, MusicBee version, skin and text scaling. For song checks, record the expected NetEase ID, observed lyrics, extras and outcome. Earlier confirmations are historical results, not passes for the new build.

Install the candidate ZIP through Add plugin, then Restart. Clear stored lyrics before testing a fresh search. Settings backup is optional; keep the repaired current settings during rollback, as explained in [INSTALL.md](../INSTALL.md).

- [ ] Confirm the plugin name and candidate version; stable builds must not show DEV BUILD.
- [ ] Fresh settings: translation on, pronunciation off, separate lines and Follow default for every language. Upgrades retain saved preferences.
- [ ] English and Simplified Chinese settings in light/dark skins, at normal and enlarged text sizes (100%, 150%, 200%). Expand Advanced and show View update; check dropdown text, wrapping, scrolling and resize.
- [ ] Change settings and interface language, then Cancel. Confirm live lookups and disk settings are unchanged. Repeat with Save and reopen; confirm the saved choices and collapsed Advanced.
- [ ] Default extras and both layouts, plus each Japanese/Korean explicit mode and Follow default. Check Cantonese original/pronunciation and the English translation override independently.
- [ ] Now Playing two-line and three-line highlighting/scrolling. Check timed, untimed and offset lyrics, near timestamps, unequal untimed line counts and missing/unaligned extras.
- [ ] Cantonese ID 409931770 and Japanese/Korean samples. Japanese ID 400875204 with pronunciation plus translation must retain original/translation when pronunciation is missing. Avoid duplicated English-only cues.
- [ ] Automatic By Your Side (ID 400875204) and Ma Meilleure Ennemie (ID 3374505659), without Custom10. Include missing duration or album tags and soundtrack attribution.
- [ ] Automatic 奇縁ロマンス (ID 2032748562), with artist tag ナナヲアカリ, original album/title tags and no Custom10. Check translation/pronunciation. With controlled fixtures, confirm an alias timeout can recover on immediate re-fetch and optional artist refusal does not block permitted lyric HTTP fallback.
- [ ] Automatic Blood Sweat & Tears (Sheryl Lee Ralph, ID 3374505661) and Fantastic (King Princess, ID 3374504658), with the original Chinese Arcane soundtrack album tag and no Custom10. Clear stored lyrics first; check originals and Chinese translations. Confirm the diagnostic report identifies LocalizedSoundtrackMatch. Include a wrong singer, live/demo variant and conflicting soundtrack sources in recorded-response checks.
- [ ] Duplicate listings, including Jonas Blue's By Your Side with an absent/nonmatching album and Lia's 鳥の詩 if still available. Check grouping and recording selection.
- [ ] A Part 1 / Part 2 pair, a remastered title and a meaningful live/remix/edit distinction. The wrong part or recording must not supply lyrics.
- [ ] A background lookup for one track while another plays. Check requested-file Custom10 ownership, desktop and phone-share song URLs.
- [ ] Missing tags, offline/slow/blocked HTTPS and HTTP opt-out. Check bounded waits, permitted fallback and no HTTP when disabled. Verify HTTPS inside MusicBee; the console host can have different defaults.
- [ ] One rollback to the previously installed plugin, then return to the candidate. Keep repaired settings and reselect preferences an older build discarded; investigate migration failures before rollback.
- [ ] Advanced diagnostic report after a lookup, plus interface switching. Confirm manual clipboard copying, the song-ID privacy note and no upload or unintended settings save.
- [ ] Manual update check: installed/current, available and failure states stay inline. Only View update opens the release page; no automatic download/install. Close settings or change interface language during a pending check. Cover rate limiting with a controlled fixture when it cannot be reproduced live.

Offline checks and simulated previews support this list but do not replace native MusicBee skin, scrolling or network checks. Keep each release's results separately from this reusable checklist.

## Historical release-note edits

Preserve assets, tags, version titles, release status and publication dates. Use the latest-release link for supersession notices so they do not need updating every version. Obtain approval for edits to published notes.
