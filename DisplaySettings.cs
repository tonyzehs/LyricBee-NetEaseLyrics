using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MusicBeePlugin
{
    public partial class NeteaseConfig
    {
        public enum UiLanguage { Automatic = 0, English = 1, SimplifiedChinese = 2 }
        public int DisplaySettingsVersion { get; set; }
        public bool? ShowTranslation { get; set; }
        public bool? ShowPronunciation { get; set; }
        public bool? SeparateLines { get; set; }
        public UiLanguage InterfaceLanguage { get; set; }
        public AccompanyingText? CantoneseOverride { get; set; }
        public AccompanyingText? JapaneseOverride { get; set; }
        public AccompanyingText? KoreanOverride { get; set; }
        public AccompanyingText? EnglishOverride { get; set; }

        [JsonIgnore] internal bool UseSeparateLines => SeparateLines ?? Format != OutputFormat.Both;
        [JsonIgnore] internal AccompanyingText DefaultContent => FromExtras(
            ShowTranslation ?? Format != OutputFormat.Original, ShowPronunciation ?? false);
        [JsonIgnore] internal bool NeedsLanguageChoice => HasPronunciation(DefaultContent) ||
            new[] { CantoneseOverride, JapaneseOverride, KoreanOverride, EnglishOverride }
                .AnyDifferent(DefaultContent);

        internal static AccompanyingText FromExtras(bool translation, bool pronunciation) => pronunciation ?
            (translation ? AccompanyingText.PronunciationAndTranslation : AccompanyingText.Pronunciation) :
            (translation ? AccompanyingText.Translation : AccompanyingText.Original);
        internal static bool HasTranslation(AccompanyingText choice) =>
            choice == AccompanyingText.Translation || choice == AccompanyingText.PronunciationAndTranslation;
        internal static bool HasPronunciation(AccompanyingText choice) =>
            choice == AccompanyingText.Pronunciation || choice == AccompanyingText.PronunciationAndTranslation;

        internal void LoadDisplaySettings(JObject stored)
        {
            var modern = stored["DisplaySettingsVersion"]?.ToString() == "1";
            ShowTranslation = ReadBoolean(stored["ShowTranslation"]) ?? Format != OutputFormat.Original;
            ShowPronunciation = ReadBoolean(stored["ShowPronunciation"]) ?? false;
            SeparateLines = ReadBoolean(stored["SeparateLines"]) ?? Format != OutputFormat.Both;
            InterfaceLanguage = ReadEnum<UiLanguage>(stored["InterfaceLanguage"]) ?? UiLanguage.Automatic;
            if (modern)
            {
                var cantonese = ReadEnum<AccompanyingText>(stored["CantoneseOverride"]);
                // Remove obsolete translation choices while preserving the user's pronunciation preference.
                CantoneseOverride = cantonese.HasValue ? (AccompanyingText?)FromExtras(false, HasPronunciation(cantonese.Value)) : null;
                JapaneseOverride = ReadEnum<AccompanyingText>(stored["JapaneseOverride"]);
                KoreanOverride = ReadEnum<AccompanyingText>(stored["KoreanOverride"]);
                var english = ReadEnum<AccompanyingText>(stored["EnglishOverride"]);
                // English has no pronunciation control; reject unsupported persisted choices independently.
                EnglishOverride = english.HasValue && !HasPronunciation(english.Value) ? english : null;
            }
            else if (Format == OutputFormat.Both || Format == OutputFormat.BothTwoLines)
            {
                JapaneseOverride = ReadEnum<AccompanyingText>(stored["JapaneseText"]);
                KoreanOverride = ReadEnum<AccompanyingText>(stored["KoreanText"]);
                CantoneseOverride = ReadBoolean(stored["EnableCantonesePronunciation"]) == true ?
                    AccompanyingText.Pronunciation : (AccompanyingText?)null;
            }
            // Legacy Translation-only becomes original + translation. Legacy Original keeps all extras off.
            DisplaySettingsVersion = 1;
        }

        internal JObject StoredDisplaySettings()
        {
            var stored = JObject.FromObject(this);
            stored["DisplaySettingsVersion"] = 1;
            // Older builds still receive valid format/content values; newer preferences remain separate.
            var choices = new[] { DefaultContent, CantoneseOverride ?? DefaultContent,
                JapaneseOverride ?? DefaultContent, KoreanOverride ?? DefaultContent, EnglishOverride ?? DefaultContent };
            var anyExtras = Array.Exists(choices, c => c != AccompanyingText.Original);
            stored["Format"] = anyExtras ? 1 : 0;
            stored["TwoLineTranslation"] = UseSeparateLines && anyExtras;
            stored["JapaneseText"] = (int)(JapaneseOverride ?? DefaultContent);
            stored["KoreanText"] = (int)(KoreanOverride ?? DefaultContent);
            stored["EnableCantonesePronunciation"] = HasPronunciation(CantoneseOverride ?? DefaultContent);
            return stored;
        }

        private static bool? ReadBoolean(JToken token) => token != null &&
            (token.Type == JTokenType.Boolean || token.Type == JTokenType.String) &&
            bool.TryParse(token.ToString(), out var value) ? (bool?)value : null;

        private static T? ReadEnum<T>(JToken token) where T : struct => token != null &&
            (token.Type == JTokenType.Integer || token.Type == JTokenType.String) &&
            Enum.TryParse(token.ToString(), true, out T value) && Enum.IsDefined(typeof(T), value) ? (T?)value : null;
    }

    internal static class ContentPreferenceExtensions
    {
        internal static bool AnyDifferent(this NeteaseConfig.AccompanyingText?[] choices, NeteaseConfig.AccompanyingText fallback) =>
            Array.Exists(choices, c => c.HasValue && c.Value != fallback);
    }
}
