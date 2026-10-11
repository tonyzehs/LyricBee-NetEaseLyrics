using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace MusicBeePlugin
{
    internal enum SongLanguage { Unknown, Cantonese, Japanese, Korean, Other }

    internal sealed class LanguagePreferences
    {
        private sealed class CachedLanguage { internal SongLanguage Language; internal DateTime Expires; }
        private readonly Dictionary<long, CachedLanguage> _cache = new Dictionary<long, CachedLanguage>();
        private readonly object _gate = new object();
        internal const int MaximumCachedLanguages = 512;

        internal SongLanguage Resolve(long id, LyricResult lyrics, LookupSession session, bool requireServiceLabel = false)
        {
            lock (_gate)
                if (_cache.TryGetValue(id, out var cached) && cached.Expires > DateTime.UtcNow)
                {
                    session.Report("Language: cached service label " + cached.Language);
                    return cached.Language;
                }
            // Original-only language choices also need classification when pronunciation is absent.
            var language = requireServiceLabel || LyricProcessor.HasLyricText(lyrics?.romalrc?.lyric) ? NeteaseApi.RequestLanguage(id, session) : null;
            if (language.HasValue && language.Value != SongLanguage.Unknown)
            {
                lock (_gate)
                {
                    if (_cache.Count >= MaximumCachedLanguages)
                        _cache.Remove(_cache.OrderBy(p => p.Value.Expires).First().Key);
                    _cache[id] = new CachedLanguage { Language = language.Value, Expires = DateTime.UtcNow.AddHours(6) };
                }
                session.Report("Language: service label " + language.Value);
                return language.Value;
            }
            var inferred = InferScriptLanguage(lyrics?.lrc?.lyric);
            session.Report("Language: " + (inferred == SongLanguage.Unknown ? "unresolved; retained translation preference" : "script fallback " + inferred));
            return inferred;
        }

        internal static SongLanguage InferScriptLanguage(string original)
        {
            if (string.IsNullOrWhiteSpace(original)) return SongLanguage.Unknown;
            // Exclude timing, metadata and credits before looking for distinctive scripts.
            var text = string.Join("\n", original.Replace("\r", "").Split('\n')
                .Select(l => Regex.Replace(l.Trim(), @"^(?:\[[0-9]+:[^\]]*\])+", ""))
                .Where(l => LyricProcessor.HasLyricText(l) && !l.StartsWith("{")));
            var kana = text.Count(c => (c >= '\u3041' && c <= '\u3096') || (c >= '\u30A1' && c <= '\u30FA') ||
                (c >= '\uFF66' && c <= '\uFF9D'));
            var hangul = text.Count(c => c >= '\uAC00' && c <= '\uD7A3');
            if (kana >= 4 && hangul == 0) return SongLanguage.Japanese;
            if (hangul >= 4 && kana == 0) return SongLanguage.Korean;
            return SongLanguage.Unknown;
        }

        internal static SongLanguage ParseServiceLanguage(JObject data)
        {
            var labels = new List<SongLanguage>();
            var blocks = data?["blocks"] as JArray;
            if (blocks == null) return SongLanguage.Unknown;
            foreach (var block in blocks.OfType<JObject>().Where(b => b["code"]?.Type == JTokenType.String && (string)b["code"] == "SONG_PLAY_ABOUT_SONG_BASIC"))
                foreach (var creative in (block["creatives"] as JArray ?? new JArray()).OfType<JObject>()
                    .Where(c => c["creativeType"]?.Type == JTokenType.String && (string)c["creativeType"] == "language"))
                    foreach (var link in ((creative["uiElement"] as JObject)?["textLinks"] as JArray ?? new JArray()).OfType<JObject>())
                    {
                        var text = link["text"]?.Type == JTokenType.String ? ((string)link["text"]).Trim().ToLowerInvariant() : null;
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        switch (text)
                        {
                            case "粤语": case "粵語": case "cantonese": case "yue": labels.Add(SongLanguage.Cantonese); break;
                            case "日语": case "日語": case "japanese": case "ja": labels.Add(SongLanguage.Japanese); break;
                            case "韩语": case "韓語": case "korean": case "ko": labels.Add(SongLanguage.Korean); break;
                            default: labels.Add(SongLanguage.Other); break;
                        }
                    }
            var unique = labels.Distinct().ToArray();
            // Explicitly multilingual service metadata must not be overridden by a script guess.
            return unique.Length == 1 ? unique[0] : unique.Length > 1 ? SongLanguage.Other : SongLanguage.Unknown;
        }

        internal static NeteaseConfig.AccompanyingText Choice(NeteaseConfig config, SongLanguage language)
        {
            switch (language)
            {
                case SongLanguage.Cantonese: return config.EnableCantonesePronunciation ?
                    NeteaseConfig.AccompanyingText.Pronunciation : NeteaseConfig.AccompanyingText.Translation;
                case SongLanguage.Japanese: return config.JapaneseText;
                case SongLanguage.Korean: return config.KoreanText;
                default: return NeteaseConfig.AccompanyingText.Translation;
            }
        }
    }
}
