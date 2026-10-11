using System;
using System.Globalization;

namespace MusicBeePlugin
{
    internal enum SettingsLabel
    {
        InterfaceLanguage, Defaults, OriginalAlways, Translation, Pronunciation, Layout,
        SeparateLines, SameLine, Overrides, FollowDefault, Cantonese, Japanese, Korean, English,
        Original, OriginalTranslation, OriginalPronunciation, AllThree, Refetch,
        MatchingNetwork, CustomizeSpelling, SmallDifferences, DefaultSpelling, HttpFallback
    }

    internal static class SettingsText
    {
        private static readonly string[] English = {
            "Interface language:", "Default display", "Original lyrics are always included.",
            "Translation", "Pronunciation", "Layout:", "Separate lines", "Same line: JP / CN",
            "Language overrides", "Follow default", "Cantonese:", "Japanese:", "Korean:", "English:",
            "Lyrics", "Lyrics + Translation", "Lyrics + Pronunciation", "Lyrics + Pronunciation + Translation",
            "Extras depend on NetEase availability. Re-fetch lyrics after changing settings.",
            "Matching and network", "Customize spelling matching", "Allow small spelling differences",
            "Default: allow small spelling differences.", "Allow HTTP fallback (unencrypted metadata)"
        };
        private static readonly string[] Chinese = {
            "界面语言：", "默认显示", "始终显示原文歌词。", "翻译", "发音", "排列方式：",
            "分行显示", "同行显示：JP / CN", "语言覆盖设置", "使用默认设置", "粤语：", "日语：", "韩语：", "英语：",
            "歌词", "歌词 + 翻译", "歌词 + 发音", "歌词 + 发音 + 翻译",
            "附加内容取决于网易云是否提供。修改设置后，请重新搜索歌词。",
            "匹配与网络", "自定义拼写匹配", "允许少量拼写差异", "默认：允许少量拼写差异。",
            "允许 HTTP 回退（元数据不加密）"
        };

        internal static bool IsChinese(NeteaseConfig.UiLanguage language, CultureInfo culture = null) =>
            language == NeteaseConfig.UiLanguage.SimplifiedChinese ||
            (language == NeteaseConfig.UiLanguage.Automatic &&
             (culture ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName == "zh");

        internal static string Get(SettingsLabel label, NeteaseConfig.UiLanguage language) =>
            (IsChinese(language) ? Chinese : English)[(int)label];
    }
}
