# Download and install

Download `LyricBee-NetEaseLyrics_[version].zip` from the [latest release](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/latest). Use the installation ZIP; the source ZIP is for development. Windows and .NET Framework 4.8 are required.

1. Close MusicBee. Back up the existing plugin DLL and settings if upgrading.
2. In MusicBee's **Preferences → Plugins**, use **Add plugin**, select the ZIP and enable **LyricBee — NetEase Lyrics**. Alternatively, replace `mb_NeteaseLyrics.dll` in MusicBee's Plugins directory while MusicBee is closed. Keep only one copy installed.
3. Set **Netease Cloud Music** in the lyric-provider priority under **Tags (2)**.
4. Choose default extras, layout, language overrides and interface language in the plugin settings. Original lyrics are always included. Fresh installations use translation on, pronunciation off, separate lines, and Follow default for every language.
5. Re-fetch lyrics after upgrading or changing display settings. MusicBee keeps previously stored lyrics until they are replaced.

The plugin retains the previous DLL/provider identity, so MusicBee recognizes an upgrade. Before the first save of older settings, it preserves `netease_config.pre-display.bak` beside `netease_config` in persistent plugin storage. To restore exact previous preferences after rollback, close MusicBee and restore that backup as `netease_config`.

# 下载与安装

从[最新发布](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/latest)下载 `LyricBee-NetEaseLyrics_[version].zip`。请选择安装 ZIP，源码 ZIP 供开发使用。需要 Windows 和 .NET Framework 4.8。

1. 关闭 MusicBee。升级前备份现有插件 DLL 和设置。
2. 在 MusicBee 的**首选项 → 插件**中使用“添加插件”，选择 ZIP 并启用 **LyricBee — NetEase Lyrics**。也可在 MusicBee 关闭时替换 Plugins 目录中的 `mb_NeteaseLyrics.dll`。只保留一份插件。
3. 在**标签 (2)**的歌词提供者优先级中选择 **Netease Cloud Music**。
4. 在插件设置中选择默认附加内容、排列方式、语言覆盖设置及界面语言。始终显示原文。首次安装默认开启翻译、关闭发音、采用分行显示，所有语言均使用默认设置。
5. 升级或修改显示设置后，重新搜索歌词。MusicBee 会保留之前保存的歌词，直到这些文本被替换。

插件保留原有 DLL 和歌词提供者标识，因此 MusicBee 会识别为升级。首次保存旧设置前，会在插件持久化存储目录中的 `netease_config` 旁保留 `netease_config.pre-display.bak`。回退后如需恢复原有完整设置，请关闭 MusicBee，再将该备份恢复为 `netease_config`。
