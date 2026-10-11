# Netease Lyric

This fork is developing **1.9.1-pre.7**, using two-line timed original/translation output by default and a spaced ` / ` separator for untimed pairs, alongside the network fallback, conservative song matching and reliability fixes for upstream 1.9.0. See [CHANGELOG.md](CHANGELOG.md) for the changes and [BUILDING.md](BUILDING.md) for building and testing. Local test builds are not a stable release. HTTP fallback is enabled by default; it sends metadata without encryption if HTTPS fails. Turn off **Allow HTTP fallback (unencrypted metadata)** to require HTTPS.

This is another lyrics retrieval plugin for music player MusicBee. It can get **synchronized** lyrics from Netease Cloud Music(网易云音乐, a cloud music service in China).

It can combine the translation to the lyrics if available, like:  
Blalalala(Original)  
Alalalala(Translation)  
is wrapped to: Blalalala/Alalalalala  
etc..

But if you don't want this function you can disable it in plugin settings.

The default format is **Original text and translation (two lines)**. An explicit saved format is retained when upgrading; choose this option in the plugin's settings to change an older selection. Each pair uses a single timed cue:

```text
[00:12.300]Original text
Translated text
[00:17.500]Next original line
Next translated line
```

Only the original line has a timestamp. The translation is a continuation of that cue, so the host can highlight and scroll both lines together. Repeating the timestamp on each line makes MusicBee select the last line instead, according to [this MusicBee forum report](https://getmusicbee.com/forum/index.php?topic=40063.0). The user confirmed the pre.5 two-line format works in MusicBee's Now Playing panel; pre.6 removes its slash marker while keeping the same timing structure. Other players may handle untimestamped continuation lines differently.

Timed two-line output adds no `/` marker; slashes already present in the lyrics are retained. The formatter matches equivalent timestamp spellings numerically, accounts for each document's offset, keeps original metadata and groups simultaneous lines into one cue. It does not guess translation matches for different times. If only the translation is untimed, it retains the timed original without inventing synchronization. The optional same-line format still uses `/` between timed paired texts.

When the original lyrics have no timestamps, the default format uses `Original text / Translated text` without language-specific labels. It pairs nonblank lines in the provider's order when counts agree, preserving original verse breaks and metadata. If counts differ but blank-separated verse counts agree, it pairs verses; within each verse, matching line counts use line pairs and differing counts combine the verse into one `original verse / translated verse` paragraph. If neither structure agrees, it retains the original text instead of forcing correspondence. This is positional formatting, not verification of translation meaning. It never manufactures timing; translation timestamps, if present without original timing, are stripped before formatting.

MusicBee may retain previously retrieved lyrics. After changing the format, re-search or replace the displayed lyrics for the track; simply restarting playback may keep the old cached text. This changes future retrieval output, not existing lyrics in audio tags.

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
