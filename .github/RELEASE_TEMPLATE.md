<!-- Template for 1.9.5 and later. Fill every placeholder and remove these comments before use.
Preparing this text is not approval to publish a release, upload assets or push tags. -->

**[Download the plugin / 下载插件](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/download/v{{VERSION}}/LyricBee-NetEaseLyrics_{{VERSION}}.zip)** — the only file needed.

## What's new

<!-- Use 3–6 plain bullets describing user-visible effects. -->
- {{CHANGE_1}}
- {{CHANGE_2}}
- {{CHANGE_3}}

## Upgrading

Your settings carry over: install the ZIP through **Preferences → Plugins → Add plugin**, then press **Restart**.
Clear and re-fetch stored lyrics to apply matching or display changes.
If you roll back, keep the repaired current settings; historical backups can restore old matching flags.

## Known issues

- {{KNOWN_ISSUES_OR_NONE_KNOWN}}

## Install / 安装

Requires MusicBee 3.x on Windows with .NET Framework 4.8. Tested with MusicBee 3.6.9668 (Microsoft Store edition).

1. Download the installation ZIP above and add it through **Preferences → Plugins → Add plugin**.
2. Enable **LyricBee — NetEase Lyrics** and select **Netease Cloud Music** in the provider priority under **Tags (2)**.
3. Choose your display settings, press **Restart** if upgrading, and re-fetch lyrics.

需要 Windows 上的 MusicBee 3.x 和 .NET Framework 4.8。已在 MusicBee 3.6.9668（Microsoft Store 版）中测试。

1. 下载上方安装 ZIP，在 **首选项 → 插件 → 添加插件** 中安装。
2. 启用 **LyricBee — NetEase Lyrics**，并在 **标签 (2)** 的提供者优先级中选择 **Netease Cloud Music**。
3. 选择显示设置；升级时点击 **Restart（重启）**，然后重新搜索歌词。

See [INSTALL.md](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/blob/v{{VERSION}}/INSTALL.md) for details / 详细说明见安装文档。

<details>
<summary>Build details</summary>

- Product version: `{{VERSION}}`; assembly/file version: `{{ASSEMBLY_VERSION}}`.
- DLL SHA-256: `{{DLL_SHA256}}`.
- Installation ZIP SHA-256: `{{ZIP_SHA256}}`.
- Source commit: [`{{COMMIT}}`](https://github.com/tonyzehs/LyricBee-NetEaseLyrics/commit/{{COMMIT}}).
- Regression checks: {{CHECK_COUNT}} (x64/x86).
- CI: [{{CI_STATUS}}]({{CI_URL}}).

</details>

**Full changes:** https://github.com/tonyzehs/LyricBee-NetEaseLyrics/compare/v{{PREVIOUS_VERSION}}...v{{VERSION}}
