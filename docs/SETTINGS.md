# Display settings

## Display settings

- Original lyrics are always included. There is no original checkbox or translation-only mode.
- **Default display:** translation on, pronunciation off. The two checkboxes select extras independently.
- On first installation, every language override starts at **Follow default**. Existing saved choices are retained when upgrading.
- **Layout:** separate lines by default, or the same line separated by ` / `. Layout applies across languages. Untimed texts retain their existing alignment and verse safeguards.
- **Language overrides:** Japanese and Korean each offer Follow default, Lyrics, Lyrics + Translation, Lyrics + Pronunciation, and Lyrics + Pronunciation + Translation. Cantonese offers Follow default, Lyrics, and Lyrics + Pronunciation; it appears below Japanese and Korean. English offers Follow default, Lyrics, and Lyrics + Translation. An override replaces the default content selection for that language; it remains active even when default extras are off.
- Pronunciation is supplied by NetEase for supported songs. Missing or unalignable extras are omitted independently; original lyrics remain available.
- English identification uses NetEase's language metadata. French and other languages are not classified as English merely because they use Latin letters. Unresolved or mixed-language labels retain the default translation preference. Existing conservative Japanese/Korean script fallback remains.
- **Spelling matching:** one Allow small spelling differences checkbox, enabled by default. Existing effective spelling preferences are retained in a separate field; legacy flags are repaired automatically on load for rollback. A settings draft affects lookups only after Save, including when switching interface language.
- **Interface language / 界面语言:** Automatic / English / 简体中文. Automatic follows the process's Windows UI culture; all Chinese UI cultures select Simplified Chinese text. Changing it preserves unsaved choices and does not change the lyrics' language.

## Updates

**Check for LyricBee updates** makes a manual request to GitHub's latest stable release endpoint. Status stays in the settings panel. **View update** appears only for a newer stable version and opens that release page only when clicked. No popup, automatic check, download or installation occurs during the check; no song metadata or account credentials are sent. Closing settings or switching interface language cancels an in-flight check. Updating still uses MusicBee's Add plugin and Restart buttons.

**检查 LyricBee 更新**仅在手动点击时访问 GitHub 的最新稳定版接口，状态直接显示在设置面板中。有更新时才会出现**查看更新**按钮，只有点击它才会打开发布页面。检查不会弹窗、自动下载或安装，也不会发送歌曲标签或账户凭据。关闭设置或切换界面语言会取消正在进行的检查。安装更新仍使用 MusicBee 的“添加插件”和 Restart（重启）按钮。

## Existing settings and rollback

Existing saved Japanese/Korean and Cantonese pronunciation choices are migrated. A previous global Lyrics-only setting keeps extras off. Translation-only becomes original + translation. Existing HTTP opt-out and spelling choices are retained.

Loading older settings repairs legacy matching flags and preserves `netease_config.pre-spelling.bak` beside `netease_config`. Existing backups are not overwritten. Keep the repaired current file when rolling back: historical backups retain their original flags. Older builds receive valid legacy format values, but cannot represent every new default/override combination. Upstream v1.8.1 still has its own weaker first-title-segment matching.

## Download and install

1. Download `LyricBee-NetEaseLyrics_[version].zip` from the [latest release](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/latest).
2. Install the ZIP through MusicBee’s **Preferences → Plugins → Add plugin**.
3. Open LyricBee’s settings and try both interface languages.
4. Apply settings, then re-fetch lyrics. MusicBee keeps previously stored lyrics until you search again.

### Upgrading

(Optional) Back up settings. Older plugin versions remain available in the release history; keeping the old DLL is unnecessary.

1. Download the latest installation ZIP.
2. Open MusicBee’s **Preferences → Plugins**, use **Add plugin**, and select the new ZIP to update LyricBee.
3. Click the **Restart** button that appears. MusicBee will restart with the updated plugin.
4. Re-fetch lyrics to apply display or matching changes to previously saved text.

Try Japanese triple-line output, Cantonese pronunciation, Korean output, English with and without translation, and French with the English override off. Also test switching layout, changing the default while an override is active, and reopening MusicBee to check saved settings.

## 下载与安装

1. 从[最新发布](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/latest)下载 `LyricBee-NetEaseLyrics_[version].zip`。
2. 通过 MusicBee 的**首选项 → 插件 → 添加插件**安装 ZIP。
3. 打开 LyricBee 设置，测试 English 和简体中文界面。
4. 应用设置后，重新搜索歌词。MusicBee 已保存的歌词不会自动替换。

### 升级

（可选）备份设置。旧版插件仍可从发布历史下载，无需保存旧 DLL。

1. 下载最新安装 ZIP。
2. 打开 MusicBee 的**首选项 → 插件**，使用“添加插件”选择新的 ZIP，以更新 LyricBee。
3. 点击出现的 **Restart（重启）**按钮。MusicBee 将重启并加载更新后的插件。
4. 重新搜索歌词，以将显示或匹配变化应用于之前保存的文本。

原文歌词始终显示。默认开启翻译、关闭发音，并采用分行显示。“语言覆盖设置”可为粤语、日语、韩语和英语指定独立选择；“使用默认设置”跟随上方的默认选项。首次安装时，所有语言均采用“使用默认设置”，升级时保留已有选择。英语不提供发音选项；粤语不提供翻译选项，并位于日语和韩语下方。拼写匹配采用单个“允许少量拼写差异”复选框。翻译和发音取决于网易云是否提供，无法安全配对的附加内容会被省略。

请测试日语三行显示、粤语发音、韩语、英语翻译开关，以及关闭英语翻译后法语歌曲是否仍能显示翻译。另请测试排列方式切换、默认选项与语言覆盖设置的关系，以及重启后的设置保存。

加载旧设置时会自动修复用于回退的旧匹配标志，并保留 `netease_config.pre-spelling.bak`，不会覆盖已有备份。回退时请保留修复后的设置；恢复历史备份也会恢复旧匹配标志。修改界面语言只会更新设置草稿，点击保存后才应用于歌词检索。旧版仅翻译模式会迁移为原文加翻译；旧版仅原文模式仍保持附加内容关闭。
