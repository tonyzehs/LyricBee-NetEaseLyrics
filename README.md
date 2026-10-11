# NetEase Lyrics for MusicBee

A lyrics retrieval plugin for [MusicBee](https://getmusicbee.com/) that downloads synchronized lyrics from NetEase Cloud Music (网易云音乐), with optional translation and pronunciation.

This fork builds on [cqjjjzr/MusicBee-NeteaseLyrics](https://github.com/cqjjjzr/MusicBee-NeteaseLyrics), originally written by Charlie Jiang. Version **1.9.2** includes the reliability, matching and display improvements developed through **1.9.1-pre.1–pre.12**. The original licenses and upstream history are retained.

## Download and install

Download [NeteaseLyrics_1.9.2.zip](https://github.com/tonyzehs/MusicBee-NeteaseLyrics/releases/download/v1.9.2/NeteaseLyrics_1.9.2.zip) from the [latest release](https://github.com/tonyzehs/MusicBee-NeteaseLyrics/releases/latest). The install ZIP contains only `mb_NeteaseLyrics.dll`; runtime dependencies are embedded. Source packages and checksums are available with each release. Older development releases are retained for reference; use 1.9.2 for normal installation.

The plugin targets Windows and .NET Framework 4.8.

1. Close MusicBee and back up the previous plugin DLL and settings if upgrading.
2. Open MusicBee's **Preferences → Plugins**, use **Add plugin**, select the ZIP and enable NetEase Lyrics. Alternatively, replace the existing DLL while MusicBee is closed.
3. In **Tags (2)**, set the lyrics provider priority to use **Netease Cloud Music**.
4. Open the plugin settings to choose the display format and language preferences.
5. Re-fetch lyrics for tracks whose display or matching you want to change.

MusicBee can keep lyrics it has already downloaded or embedded. Installing an update or restarting playback does not replace that stored text. Clear or replace incorrect lyrics and search again to evaluate a new lookup.

## Feature additions

### Original, translation and pronunciation displays

The global format offers:

- **Lyrics only**
- **Lyrics + extras (same line: JP / CN)**
- **Translation only**
- **Lyrics + extras (separate lines)** — the default

Separate-line timed output groups the original and its extras under one timestamp:

```text
[00:12.300]Original lyrics
Translated lyrics
[00:17.500]Next original line
Next translated line
```

With pronunciation and translation selected, a cue can contain three lines:

```text
[00:12.300]Original lyrics
Pronunciation
Translated lyrics
```

The continuation lines belong to the original cue; the plugin does not repeat its timestamp or add a slash marker to separate-line output. Two-line scrolling/highlighting has been confirmed in MusicBee's Now Playing panel. Check three-line rendering in the panel you use, since rendering is controlled by MusicBee.

### Independent language preferences

| Language | Choices | Default |
| --- | --- | --- |
| Cantonese | Enable pronunciation checkbox | Off |
| Japanese | Lyrics; Lyrics + Translation; Lyrics + Pronunciation; Lyrics + Pronunciation + Translation | Lyrics + Translation |
| Korean | Lyrics; Lyrics + Translation; Lyrics + Pronunciation; Lyrics + Pronunciation + Translation | Lyrics + Translation |

These preferences apply to the two combined global formats. **Lyrics only** and **Translation only** override them for all languages. The language-specific **Lyrics** choice returns the original unchanged.

Pronunciation comes from NetEase's `romalrc` track; this plugin does not generate readings locally. Cantonese pronunciation replaces the translation extra when enabled. For Japanese/Korean triple-line output, a missing or safely unalignable extra is omitted while the other is retained. If both extras are missing, the original is retained. Pronunciation-only does not substitute translation when pronunciation is unavailable. Translation-only also falls back to the original when no usable translation exists.

The plugin uses NetEase's song-language label, with conservative kana/Hangul detection when that label is unavailable. Han characters alone cannot distinguish Cantonese from Mandarin. Unsupported, multilingual or unresolved language labels retain the general translation behavior. Pronunciation availability varies by song; no pronunciation setting is offered for other languages.

Saved preferences carry over between recent versions. The defaults in the table apply when no choice has been saved; upgrading does not turn off a pronunciation mode you previously selected. Back up settings before rolling back: older builds may discard newer choices when saving, and pre.10/pre.11 fall back to translation for the new language-specific Lyrics choice.

### Untimed lyrics

When original lyrics have no timestamps, compatible lines are displayed as `Original / Translation`, or `Original / Pronunciation / Translation`. There are no language-specific labels. Original verse breaks and section markers are retained.

If line counts differ but verse structure agrees, each verse's original lines are followed by its available pronunciation/translation lines. Incompatible structures retain the original rather than forcing correspondence. The plugin never invents timestamps or borrows translation timing for an untimed original.

### More careful automatic matching

Title, artist, album and available duration contribute to matching. The plugin rejects contradictory or ambiguous recordings rather than choosing an unrelated song simply because its duration is close.

- Numbered titles remain distinct; explicit **Pt. II** and **Part 2** can agree.
- Live, remix, radio-edit and other meaningful version labels remain significant.
- Recognized remaster notes can be ignored. Mono/stereo and album-version notes require known, closely matching durations.
- Compatible duplicate catalogue listings are grouped. If the chosen listing lacks lyrics or requested extras, another compatible listing can be tried.
- A terminal soundtrack attribution such as **(from the series Arcane League of Legends)** can be ignored only when complete artist credits, album, duration and the named work corroborate it. This fixes recordings such as Stromae/Pomme's **Ma Meilleure Ennemie** without removing meaningful title words indiscriminately.

Missing tags, translated titles or unresolved editions can still prevent a confident match. The manual song override remains available.

### Select a specific NetEase song

Set the requested music file's **Custom10** tag to a NetEase song URL or `netease=ID`, then re-search:

```text
https://music.163.com/#/song?id=29126914
https://music.163.com/song?id=29126914
https://y.music.163.com/m/song?id=29126914
netease=29126914
```

Desktop and phone-share song links are supported. Album, playlist, artist and unrelated-domain links are rejected. The override belongs to the file being searched, including background lookups while a different song is playing.

### Matching and network options

**Customize spelling matching** exposes **Allow small spelling differences**. With customization off, small spelling differences are allowed by default; the readable hint explains that state. Number, artist and recording checks still apply in either mode.

**Allow HTTP fallback (unencrypted metadata)** is enabled by default. HTTPS is tried first. Plain HTTP is used only after a transport failure on the legacy HTTPS interface; a secure HTTP/API refusal prevents that downgrade for the lookup. Turn this option off to require HTTPS. Optional language metadata always uses HTTPS.

No login or account cookie is required. Anonymous server cookies are not stored and replayed between requests. The plugin supplies lyric text to MusicBee; MusicBee handles display and storage.

## Build and development history

See [BUILDING.md](BUILDING.md) for the Windows/.NET build instructions and validation details, and [CHANGELOG.md](CHANGELOG.md) for the step-by-step changes. Run `build.ps1` from the repository root to build, test and package the plugin.

The latest build passes **246 regression checks in both 64-bit and 32-bit test processes**, using the packaged DLL with embedded dependencies. Fixtures cover the MusicBee callback, matching, timing, configuration and controlled network failures; they do not replace native MusicBee testing or guarantee NetEase availability in every region.

The [release history](https://github.com/tonyzehs/MusicBee-NeteaseLyrics/releases) groups development into seven milestones. Each release uses its original build packages; smaller updates are included in the next milestone. Padded prerelease tags keep the listing in numeric order. Tags identify archived source snapshots, and release dates reflect their publication. Source packages include the documentation from each build; this README describes 1.9.2.

| Milestone | Included development steps | Main changes |
|---|---|---|
| [1.9.1-pre.1](https://github.com/tonyzehs/MusicBee-NeteaseLyrics/releases/tag/v1.9.1-pre01) | pre.1 | Reliability and build foundation |
| [1.9.1-pre.4](https://github.com/tonyzehs/MusicBee-NeteaseLyrics/releases/tag/v1.9.1-pre04) | pre.2, pre.3, pre.4 | Safer matching and network recovery |
| [1.9.1-pre.7](https://github.com/tonyzehs/MusicBee-NeteaseLyrics/releases/tag/v1.9.1-pre07) | pre.5, pre.6, pre.7 | Separate-line bilingual lyrics |
| [1.9.1-pre.9](https://github.com/tonyzehs/MusicBee-NeteaseLyrics/releases/tag/v1.9.1-pre09) | pre.8, pre.9 | Recording identity, alignment and compatibility |
| [1.9.1-pre.10](https://github.com/tonyzehs/MusicBee-NeteaseLyrics/releases/tag/v1.9.1-pre10) | pre.10 | Pronunciation and three-line lyrics |
| [1.9.1-pre.11](https://github.com/tonyzehs/MusicBee-NeteaseLyrics/releases/tag/v1.9.1-pre11) | pre.11 | Soundtrack attribution matching |
| [1.9.2](https://github.com/tonyzehs/MusicBee-NeteaseLyrics/releases/tag/v1.9.2) | pre.12, 1.9.2 | Language choices and translation defaults |

## 网易云音乐歌词

这是用于 MusicBee 的网易云音乐同步歌词插件，基于 [Charlie Jiang 的原版](https://github.com/cqjjjzr/MusicBee-NeteaseLyrics)继续改进，可显示原文、翻译以及网易云提供的发音。

从本仓库的[最新发布](https://github.com/tonyzehs/MusicBee-NeteaseLyrics/releases/latest)下载 ZIP，在 MusicBee 的**首选项 → 插件**中通过“添加插件”安装并启用，在**标签 (2)** 中调整歌词提供者优先级。更新前请备份旧 DLL 和设置。

默认以两行显示带时间戳的原文与翻译；日语和韩语各自可选择“原文”“原文 + 翻译”“原文 + 发音”“原文 + 发音 + 翻译”，粤语发音默认关闭。发音由网易云提供，插件不自行生成。缺少可用附加文本时保留其他可用文本，最后回退到原文。近期版本保留已保存的个人选项。

如需指定歌曲，将该文件的 **Custom10** 标签设为歌曲链接或 `netease=歌曲ID`，然后重新搜索。支持电脑与手机分享的歌曲链接。更新或更改显示设置后，请重新获取歌词；MusicBee 已缓存或写入文件的旧歌词不会自动被替换。

## Bug fixes, maintenance and other improvements over upstream v1.9.0-pre

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
- Shorten clipped settings descriptions, retain explicit saved preferences, improve the disabled spelling option's readability, and offer the four independent Japanese/Korean content choices.

The project remains under [Apache 2.0](LICENSE), with the retained [Zony MIT notice](LICENSE-Zony) for adopted API code. Credit for the original plugin and its upstream history belongs to Charlie Jiang and the upstream contributors.
