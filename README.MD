# Netease Lyric

This fork is developing **1.9.1-pre.10**, adding language-specific pronunciation choices and optional three-line lyrics to the matching, alignment and compatibility fixes from pre.9. See [CHANGELOG.md](CHANGELOG.md) for changes and [BUILDING.md](BUILDING.md) for building and testing. Local test builds are not a stable release. HTTP fallback is enabled by default for transport failures, including timeouts; it sends metadata without encryption. A secure API refusal blocks HTTP fallback for that lookup. Turn off **Allow HTTP fallback (unencrypted metadata)** to require HTTPS.

Automatic matching keeps title numbers distinct and recognizes explicit `Pt. II` / `Part 2` forms. Recognized remaster annotations do not change title identity. Mono, stereo and album-version annotations can be ignored only when local and service durations are known and within two seconds; live, remix, radio-edit and original-mix labels remain significant.

Different catalogue IDs may be grouped when their normalized titles, complete artist credits and version labels agree and every pair of service durations is within two seconds. Album similarity, then local duration difference, then a stable ID order chooses among those listings. This is a compatibility heuristic, not proof of identical recordings. Missing lyrics or requested translations can trigger another listing's lyric download, with at most three listings attempted. A usable original is retained if translation retries fail. Custom10 remains an explicit single-song override.

The previous **Use legacy matching strategy** checkbox is now **Customize spelling matching**. Saved choices are retained, but all callback lookups use available album and duration tags. With customization off, small spelling differences are allowed; with it on, the spelling checkbox controls this allowance. Number and recording checks always apply. MusicBee trace messages describe grouping, annotation handling and alternate lyric attempts without logging raw song tags or local paths. Lookups allow at most nine search requests and nine lyric requests, within the existing 30-second deadline.

This is another lyrics retrieval plugin for music player MusicBee. It can get **synchronized** lyrics from Netease Cloud Music(网易云音乐, a cloud music service in China).

It combines original lyrics with available translations or pronunciation supplied by NetEase.

Choose the output and language preferences in the plugin settings.

The default format is **Original and extra text (separate lines)**, with translation selected for Japanese and Korean and Cantonese pronunciation off. An explicit saved format is retained when upgrading; choose this option in the plugin's settings to change an older selection. Each pair uses a single timed cue:

```text
[00:12.300]Original text
Translated text
[00:17.500]Next original line
Next translated line
```

Only the original line has a timestamp. The translation is a continuation of that cue, so the host can highlight and scroll both lines together. Repeating the timestamp on each line makes MusicBee select the last line instead, according to [this MusicBee forum report](https://getmusicbee.com/forum/index.php?topic=40063.0). The user confirmed the pre.5 two-line format works in MusicBee's Now Playing panel; pre.6 removes its slash marker while keeping the same timing structure. Other players may handle untimestamped continuation lines differently.

Timed two-line output adds no `/` marker; slashes already present in the lyrics are retained. Both bilingual formats share numeric parsing, timestamp ordering and translation alignment. Exact matches have priority; remaining translations can use their unique nearest cue within 50 ms, preserving order. Equidistant matches and near duplicates of an occupied cue are skipped. Approximate matches require a majority of the smaller document's usable cue groups to be paired. Raw and offset-adjusted timings are compared by paired count, total error and maximum error; equal-quality conflicting clocks retain common exact pairs only. Original playback offsets remain intact. If only the translation is untimed, the timed original is retained without invented synchronization. The optional same-line timed format uses `/` between paired texts.

When the original lyrics have no timestamps, both bilingual formats use `Original text / Translated text` without language-specific labels. They pair usable lines in provider order when counts agree, preserving original breaks, metadata and section markers. If line counts differ but blank-separated verse counts agree, equal-length verses pair line by line; unequal verses show their original lines, a blank line, then their translation lines. Nothing is flattened into a paragraph. If neither structure agrees, the original is retained instead of forcing correspondence. This is positional formatting, not semantic verification, and never manufactures timing. Translation timestamps are stripped if the original is untimed. Metadata-only translations and slash-only placeholders are treated as absent, including translation-only mode.

Pronunciation choices apply only to the two combined output formats:

- **Cantonese — Enable pronunciation:** original plus pronunciation when checked; translation behavior when unchecked. Default: unchecked.
- **Japanese** and **Korean:** independent dropdowns for **Translation**, **Pronunciation**, and **Pronunciation & Translation**. Default: Translation.
- **Original only** and **Translation only** take priority over these choices. Translation-only retains the original when no usable translation exists.

For timed separate-line output, the combined pronunciation choice produces:

```text
[00:12.300]Original text
Pronunciation
Translated text
```

One timestamp owns the whole group. If either extra is absent, duplicated or cannot align safely, keep the other extra; if neither works, preserve the original. This applies per cue as well as per track. Pronunciation-only falls back to original, without substituting translation. Untimed output keeps spaced ` / ` pairs where line counts agree, or adjacent original/pronunciation/translation verse blocks when matching verses have unequal line counts. Incompatible structures are omitted without inventing timestamps. The same-line combined option retains its **JP / CN** label and uses spaced separators when pronunciation is selected.

These are NetEase-provided pronunciation tracks (`romalrc`), not readings generated by this plugin. Each extra aligns independently against the original using the existing exact/50 ms, offset and ambiguity rules. The newer word-timed `yromalrc` track is not mixed with ordinary LRC timing.

Language selection uses NetEase's song-language label. An optional anonymous HTTPS metadata request is made only when pronunciation is supplied and a non-default language choice is active. It has a two-second maximum, at most one request per lookup, and counts inside the existing nine-request lyric allowance and 30-second deadline. Known labels are cached by song ID for six hours, with at most 512 entries; lyric text is not cached by this feature. Failures are not cached. The default translation choices make no extra language request.

If language metadata is unavailable or lacks a language, distinctive Japanese kana or Korean Hangul can identify those languages conservatively. Han-only lyrics cannot distinguish Cantonese from Mandarin, so Cantonese needs a service label. Explicit unsupported or multilingual labels retain translation behavior rather than being guessed from script. The plugin offers no pronunciation control for other languages. Availability varies by recording: Japanese, Korean and Cantonese tracks were verified, but this is not a promise that every song in those languages has pronunciation.

Changing these preferences affects future downloads. Re-fetch the lyrics in MusicBee to see the change. The settings panel is implemented; a shortcut in MusicBee's lyrics right-click menu is not included. Three-line highlighting still needs confirmation in MusicBee's actual Now Playing display; the user's earlier two-line confirmation does not establish three-line support.

Two-line preferences are saved as an older-compatible `Format=1` plus `TwoLineTranslation=true`. Existing `Format=3` files migrate when loaded, keeping a `.pre9.bak` copy before replacement. If migration cannot save safely, the loaded preferences remain active and the trace reports the failure; resolve that before rolling back. Older versions ignore the new flag and show same-line lyrics. Saving settings in an older version can remove the flag, so choose two-line output again after returning to this build if necessary.

MusicBee may retain previously retrieved lyrics. After upgrading or changing the format, clear incorrect stored lyrics and re-search or replace the displayed lyrics for the track; simply restarting playback may keep the old cached text. These fixes affect future retrieval, not lyrics already stored in audio tags. Back up stored lyrics before replacing them if you want to keep them.

## Usage
To use this plugin, download it from links below:  
For users who is not in China:  
[OneDrive](https://1drv.ms/f/s!AicHZ6DLvCtX6Qp6KQfEppoQtjLG)  
For users who is in China:  
[BaiduYun](https://pan.baidu.com/s/1ZL9dyrAczhPSMvMtvvG5yA?pwd=vede) , passcode: `vede`
Install(via "Add plugin" button) and enable it in the `Plugin` tab in the preferences.  
And adjust the retrieving provider priority of "Netease Cloud Music" in the Tags(2) Tab.  
If you want a specific song's lyric from NetEase Cloud Music to be matched, you may set the `custom10` tag to the song URL like `https://music.163.com/#/song?id=29126914` (You can directly copy URL like this from the Netease website or client) or `netease=123123` (where "123123" should be your song id) in the music and re-search lyrics.

## For non-Chinese users 
And this plugin is also useful for people who aren't from China as Netease CloudMusic also has bunch of songs in other languages. You can disable the translation in the plugin settings.

# 网易云音乐歌词

这是MusicBee另一个获取歌词的插件。可以从网易云音乐获取**同步歌词**。  

该插件可以合并歌词翻译（如果有的话）到歌词。例如：  
巴拉巴拉巴拉巴（原句）  
阿啦啦啦啦啦（翻译）  
会被合并为：巴拉巴拉巴拉巴/阿啦啦啦啦啦  

如果不想要这个功能可以在设置里面的插件设置中关闭。
如果想设置网易云音乐中特定的歌曲，可以在 `标签 (2)` 标签页给歌曲设定 `custom10` 标签，内容为曲目的 URL，比如 `https://music.163.com/#/song?id=29126914`（可以直接从网易云网站或客户端复制下来），或者 `netease=123123`，其中 123123 是曲目的 ID，并重新搜索歌词。

## 使用
从[度盘](https://pan.baidu.com/s/1ZL9dyrAczhPSMvMtvvG5yA?pwd=vede)下载（提取码 `vede`），并在首选项的 `插件` 中安装（“添加插件”按钮）、启用插件，在 `标签 (2)` 标签页调整歌词的提供者优先级。
