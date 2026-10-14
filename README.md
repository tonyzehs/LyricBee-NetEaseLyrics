<img align="right" src="docs/assets/icons/lyricbee.svg" alt="LyricBee logo" width="64" height="64">

# LyricBee — NetEase Lyrics for MusicBee

EN | [ZH（简体中文）](README.zh-CN.md) | [Download & install ↓](#download-and-install)

LyricBee brings timed lyrics from NetEase Cloud Music (网易云音乐) to [MusicBee](https://getmusicbee.com/). It can show translations and pronunciation alongside the original lyrics.

Based on [Charlie Jiang’s NetEase Lyrics plugin](https://github.com/cqjjjzr/MusicBee-NeteaseLyrics), LyricBee is now maintained as a separate project. It keeps the original licenses and Git history. Please use this repository for [bug reports](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/issues).

## Feature additions

[![Japanese lyrics, pronunciation and Chinese translation highlighted together in MusicBee's Now Playing panel](docs/screenshots/japanese-three-line.webp)](docs/screenshots/japanese-three-line.webp)

### Original, translation and pronunciation displays

Original lyrics are always shown. In the plugin settings, choose whether to add:

- **Translation** — on by default.
- **Pronunciation** — off by default.
- **Layout** — **Separate lines** by default, or **Same line: JP / CN** with a spaced ` / ` separator.

Turn both extras off to show just the original lyrics. In MusicBee’s Now Playing panel, the original, pronunciation and translation can scroll and highlight together. Text wrapping and appearance depend on your MusicBee panel and skin.

Translations and pronunciation come from NetEase, so availability varies by song. If one is missing or its timing cannot be matched, LyricBee shows the remaining lines. Choosing pronunciation alone gives the original lyrics when a reading is unavailable.

### Language overrides

Use **Language overrides** to choose different content for particular languages. Each starts at **Follow default**, which uses the Translation and Pronunciation checkboxes above it.

| Language | Other choices |
| --- | --- |
| Japanese | Lyrics; Lyrics + Translation; Lyrics + Pronunciation; Lyrics + Pronunciation + Translation |
| Korean | Lyrics; Lyrics + Translation; Lyrics + Pronunciation; Lyrics + Pronunciation + Translation |
| Cantonese | Lyrics; Lyrics + Pronunciation |
| English | Lyrics; Lyrics + Translation |

A language choice replaces the default checkboxes for that language. For example, you can keep translations on for most songs and choose original lyrics only for English. Layout applies to every language.

LyricBee uses NetEase’s language information and can also recognize Japanese kana and Korean text. When it cannot tell the language, it uses your defaults. The English setting applies to songs identified as English; French and other languages keep their own default display.

### English and Simplified Chinese interface

**Interface language / 界面语言** offers **Automatic / 自动**, **English**, and **简体中文**. Automatic follows your Windows display language and uses Simplified Chinese for Chinese languages. Switching the interface language keeps your current choices. The lyrics stay in their original language.

### Two-line examples

Click either preview to see it at full size.

| Japanese: lyrics and Chinese translation | Cantonese: lyrics and pronunciation |
| :---: | :---: |
| [<img src="docs/screenshots/japanese-two-line.webp" alt="Japanese lyrics and Chinese translation highlighted together in MusicBee" width="400">](docs/screenshots/japanese-two-line.webp) | [<img src="docs/screenshots/cantonese-two-line.webp" alt="Cantonese lyrics and romanized pronunciation highlighted together in MusicBee" width="400">](docs/screenshots/cantonese-two-line.webp) |

### Original-only example

English lyrics with Translation and Pronunciation turned off. Click to see the full-size screenshot.

[<img src="docs/screenshots/english-single-line.webp" alt="Original English lyrics highlighted in MusicBee" width="800">](docs/screenshots/english-single-line.webp)

### Untimed lyrics

Lyrics without timestamps use `Original / Translation` or `Original / Pronunciation / Translation`. Verse breaks and section labels are kept.

When the texts have different line counts but matching verses, each verse shows the original first, followed by its pronunciation and translation. If the verses cannot be paired, the original is shown on its own. Scrolling with the music requires timed original lyrics.

### More careful automatic matching

LyricBee checks the title, artists, album and song length to find the right recording. If the results leave more than one likely recording, it returns no lyrics so you can choose the song yourself.

- Numbered songs stay separate. `Pt. II` and `Part 2` can mean the same part.
- Words such as Live, Remix and Radio Edit still count when choosing a version.
- Remaster notes can be ignored. Mono, stereo and album-version notes can be ignored when the song lengths are close.
- When NetEase lists a recording more than once, LyricBee can try another listing for missing lyrics, translation or pronunciation.
- Soundtrack searches can handle an added title ending such as `(from the series Arcane League of Legends)`. A Chinese soundtrack album tag can also match an English listing when the title, full artist list and length agree.
- Artist names in different languages can match through NetEase’s own aliases. The title and album must agree, and song lengths must be within two seconds.

Some songs still need a [manual song link](#select-a-specific-netease-song), especially when tags are missing or several recordings look alike. See [UPSTREAM-CHANGES.md](UPSTREAM-CHANGES.md#matching-and-compatible-listings) for the detailed matching rules and their limits.

### Real-song examples

These three songs have been checked in MusicBee using their existing tags and an empty Custom10 field.

| Song | Earlier problem | Result |
| --- | --- | --- |
| [By Your Side](https://music.163.com/song?id=400875204) — 澤田かおり and 服部隆之, from *Mobile Suit Gundam The Origin* | The Japanese song received unrelated Cantonese lyrics. | Finds the Japanese recording, including when both the singer and composer are in the artist tag. |
| [Ma Meilleure Ennemie](https://music.163.com/song?id=3374505659) — Stromae and Pomme, from *Arcane Season 2* | NetEase adds `(from the series Arcane League of Legends)` to the title, which stopped the song matching. | Finds the recording with the shorter local title when the artists, album and length agree. |
| [奇縁ロマンス](https://music.163.com/song?id=2032748562) — ナナヲアカリ, listed as 七音阿卡莉 on NetEase | The Japanese and Chinese artist names were treated as different performers. | Uses NetEase’s artist aliases to match the Japanese tag to the Chinese name. |

Re-fetch affected tracks to replace lyrics MusicBee already saved.

### Select a specific NetEase song

Put a NetEase song link or `netease=ID` in the music file’s **Custom10** tag, then search for lyrics again:

```text
https://music.163.com/#/song?id=29126914
https://music.163.com/song?id=29126914
https://y.music.163.com/m/song?id=29126914
netease=29126914
```

Both desktop and phone-share song links work. Use a link to the song itself, rather than an album, playlist or artist page.

### Matching, network and privacy

**Allow small spelling differences** is on by default. Turn it off for exact spelling. Song numbers, artists and recording versions are still checked.

**Allow HTTP fallback (unencrypted metadata)** is also on by default. LyricBee tries HTTPS first. If that connection fails or NetEase has a temporary server error, it can try HTTP. HTTP sends song information without encryption; turn this setting off to require HTTPS.

Searches send the title, artists and album to NetEase. Fetching lyrics sends the NetEase song ID. LyricBee works without a NetEase account. MusicBee handles how the returned lyrics are displayed and saved.

### Check for LyricBee updates

Click **Check for LyricBee updates** in the plugin settings. The result appears there. If a newer version is available, click **View update** to open its release page.

Update checks are manual. They send no song information to GitHub, and updates are installed only when you download and add the ZIP yourself.

## Download and install

Download `LyricBee-NetEaseLyrics_[version].zip` from the [latest release](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/latest). Choose this installation ZIP; GitHub’s Source code archives are for development. The ZIP includes the plugin, licenses and installation guide. `SHA256SUMS.txt` is available if you want to check the download.

Requires MusicBee 3.x on Windows with .NET Framework 4.8. Tested with MusicBee 3.6.9668 (Microsoft Store edition).

1. Open MusicBee’s **Preferences → Plugins**, click **Add plugin**, select the ZIP and enable **LyricBee — NetEase Lyrics**.
2. Under **Tags (2)**, put **Netease Cloud Music** in the lyrics provider priority list.
3. Open the plugin settings to choose translations, pronunciation, layout and interface language.
4. Search for lyrics on the songs you want to update.

### Upgrading

(Optional) Back up your settings first. Older versions are available in the [release history](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases).

1. Download the latest installation ZIP.
2. Open **Preferences → Plugins → Add plugin** and select the new ZIP.
3. Click the **Restart** button that appears. MusicBee will restart with the updated plugin.
4. Clear and re-fetch saved lyrics to use the new matching or display settings.

Your settings carry over. If you return to an older plugin, keep the current settings file: restoring an old backup can bring back earlier matching problems. See [INSTALL.md](INSTALL.md) for more about older settings.

MusicBee keeps lyrics it has downloaded or written into music files. Re-fetching replaces that saved text; restarting playback alone leaves it in place.

## Troubleshooting

- If lyrics are missing, check the provider priority under **Tags (2)**, then try searching again. You can also put the correct NetEase song link in **Custom10**.
- If the wrong song appears, set **Custom10** to the correct song link or `netease=ID`, then clear and re-fetch the lyrics.
- If old lyrics keep showing, clear the saved text in MusicBee and fetch it again.
- To report a problem, include the NetEase song link and your MusicBee and LyricBee versions. Reproduce the problem, then open the plugin settings → **Advanced… → Copy diagnostic report**. Paste the report into your issue. It is copied to your clipboard, not uploaded automatically. It contains song IDs that may reveal your listening history; lyrics, tags, file paths and account details are left out.

## Fixes over upstream v1.9.0

- Handles failed network requests and missing tags without crashing.
- Checks more song details to reduce wrong lyrics, and tries duplicate listings when lyrics are missing.
- Keeps original lyrics, translations and pronunciation together, with fixes for timing and untimed text.
- Limits how long a search can wait and tries another connection when possible.
- Offers English and Simplified Chinese settings, with separate choices for content and layout.
- Smaller plugin: removing unused dependencies reduced the DLL from about 4 MB to 1.1 MB.

See [UPSTREAM-CHANGES.md](UPSTREAM-CHANGES.md) for the full list.

## Build and development history

See [BUILDING.md](BUILDING.md) to build and test the plugin on Windows, and [CHANGELOG.md](CHANGELOG.md) for changes by version. Development takes place on `dev`; stable updates go to `master`.

The [release history](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases) keeps the earlier versions. The [previous fork](https://github.com/tonyzehs/MusicBee-NeteaseLyrics) also preserves its original download links.

<details>
<summary>Earlier release milestones</summary>

| Milestone | Included development steps | Main changes |
|---|---|---|
| [1.9.1-pre.1](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.1-pre01) | pre.1 | Fewer crashes and a repeatable build |
| [1.9.1-pre.4](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.1-pre04) | pre.2, pre.3, pre.4 | Better matching and network recovery |
| [1.9.1-pre.7](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.1-pre07) | pre.5, pre.6, pre.7 | Separate-line bilingual lyrics |
| [1.9.1-pre.9](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.1-pre09) | pre.8, pre.9 | Duplicate recordings, timing and older settings |
| [1.9.1-pre.10](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.1-pre10) | pre.10 | Pronunciation and three-line lyrics |
| [1.9.1-pre.11](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.1-pre11) | pre.11 | Soundtrack title matching |
| [1.9.2](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.2) | pre.12, 1.9.2 | Language choices and translation defaults |
| [1.9.3](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.3) | 1.9.3 | Standalone LyricBee project and display name |
| [1.9.4](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.4) | 1.9.4-test.1, test.2, 1.9.4 | Bilingual settings, independent extras, language overrides and simpler spelling controls |
| [1.9.5](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.5) | 1.9.5-test.1–test.4, 1.9.5 | Automatic settings repair, matching and timing fixes, and diagnostic reports under Advanced |
| [1.9.6](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.6) | 1.9.6-test.1–test.4, 1.9.6 | Manual update checking, improved diagnostic reports and automatic settings layout |
| [1.9.7](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v1.9.7) | 1.9.7-test.1–test.2, 1.9.7 | Artist names in different languages |

</details>

LyricBee is maintained by [Tony Y](https://github.com/tonyzehs). Thanks to Charlie Jiang and the upstream contributors for the original plugin. The project uses [Apache 2.0](LICENSE), with [Zony’s MIT notice](LICENSE-Zony) for the adopted API code. See [NOTICE](NOTICE) for credits.
