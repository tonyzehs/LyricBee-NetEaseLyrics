# Download and install

Download `LyricBee-NetEaseLyrics_[version].zip` from the [latest release](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/latest). Use the installation ZIP; the source ZIP is for development. Windows and .NET Framework 4.8 are required.

1. In MusicBee’s **Preferences → Plugins**, use **Add plugin**, select the ZIP and enable **LyricBee — NetEase Lyrics**.
2. Set **Netease Cloud Music** in the lyric-provider priority under **Tags (2)**.
3. Choose default extras, layout, language overrides and interface language in the plugin settings. Original lyrics are always included. Fresh installations use translation on, pronunciation off, separate lines, and Follow default for every language.
4. Re-fetch lyrics after upgrading or changing display settings. MusicBee keeps previously stored lyrics until they are replaced.

### Upgrading

(Optional) Back up your settings before installing. Previous plugin versions are available in the [release history](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases), so keeping the old DLL is unnecessary.

1. Download the latest installation ZIP.
2. Open MusicBee’s **Preferences → Plugins**, use **Add plugin**, and select the new ZIP to update LyricBee.
3. Click the **Restart** button that appears. MusicBee will restart with the updated plugin.
4. Re-fetch lyrics to apply display or matching changes to previously saved text.

Older settings are repaired automatically when the plugin loads, with the previous file retained as `netease_config.pre-spelling.bak`. Keep the repaired settings when rolling back; restoring a historical backup also restores its old matching flags. Upstream v1.8.1 retains its own weaker matching behavior.

# 下载与安装

从[最新发布](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/latest)下载 `LyricBee-NetEaseLyrics_[version].zip`。请选择安装 ZIP，源码 ZIP 供开发使用。需要 Windows 和 .NET Framework 4.8。

1. 在 MusicBee 的**首选项 → 插件**中使用“添加插件”，选择 ZIP 并启用 **LyricBee — NetEase Lyrics**。
2. 在**标签 (2)**的歌词提供者优先级中选择 **Netease Cloud Music**。
3. 在插件设置中选择默认附加内容、排列方式、语言覆盖设置及界面语言。始终显示原文。首次安装默认开启翻译、关闭发音、采用分行显示，所有语言均使用默认设置。
4. 升级或修改显示设置后，重新搜索歌词。MusicBee 会保留之前保存的歌词，直到这些文本被替换。

### 升级

（可选）备份设置。旧版插件可从[发布历史](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases)下载，无需保存旧 DLL。

1. 下载最新安装 ZIP。
2. 打开 MusicBee 的**首选项 → 插件**，使用“添加插件”选择新的 ZIP，以更新 LyricBee。
3. 点击出现的 **Restart（重启）**按钮。MusicBee 将重启并加载更新后的插件。
4. 重新搜索歌词，以将显示或匹配变化应用于之前保存的文本。

插件加载时会自动修复旧设置，并将原文件保留为 `netease_config.pre-spelling.bak`。回退时请保留修复后的设置；恢复历史备份也会恢复其中的旧匹配选项。上游 v1.8.1 本身仍采用较弱的匹配规则。
