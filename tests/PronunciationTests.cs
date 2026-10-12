using MusicBeePlugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    private static string PronunciationJson(string original = "[00:01]にほんごの歌詞", string roma = "[00:01]roman", string translation = "[00:01]译文") =>
        JsonConvert.SerializeObject(new { code = 200, lrc = new { lyric = original }, romalrc = new { lyric = roma }, tlyric = new { lyric = translation } });
    private static string Wiki(params string[] languages) => JsonConvert.SerializeObject(new
    {
        code = 200, data = new { blocks = new[] { new { code = "SONG_PLAY_ABOUT_SONG_BASIC", creatives = new[] {
            new { creativeType = "language", uiElement = new { textLinks = languages.Select(l => new { text = l }).ToArray() } }
        } } } }
    });
    private static Plugin PronunciationPlugin(Fixture handler, string preferences)
    {
        var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
        SetConfig(plugin, "{\"Format\":1,\"TwoLineTranslation\":true," + preferences + "}");
        return plugin;
    }
    private static Fixture PronunciationFixture(string lyrics, string wiki) => new Fixture((request, _) =>
        Json(request.RequestUri.AbsolutePath.Contains("/about/") ? wiki : lyrics));
    private static string PronunciationMerge(string original, string roma, string translation, out int used) =>
        LyricProcessor.MergePronunciation(original, roma, translation, true, null, out used);

    private static void PronunciationTests()
    {
        Test("Pronunciation is deserialized from the existing lyric request", () =>
        {
            var handler = new Fixture((_, __) => Json(PronunciationJson()));
            using (var session = Session(handler)) Equal("[00:01]roman", NeteaseApi.RequestLyric(101, session).romalrc.lyric);
            Equal(1, handler.Calls.Count);
        });
        Test("Legacy lyric fallback explicitly requests pronunciation", () =>
        {
            var handler = new Fixture((_, number) => Json(number == 1 ? "{\"code\":200}" : PronunciationJson()));
            using (var session = Session(handler)) Equal("[00:01]roman", NeteaseApi.RequestLyric(101, session).romalrc.lyric);
            True(handler.Calls[1].Contains("rv=-1"));
        });
        Test("Defaults retain two-line translation without a language request", () =>
        {
            var handler = PronunciationFixture(PronunciationJson(), Wiki("日语"));
            Equal("[00:01.000]にほんごの歌詞\n译文", Lookup(PronunciationPlugin(handler, "\"JapaneseText\":0")));
            Equal(1, handler.Calls.Count);
        });
        Test("Cantonese checkbox adds server pronunciation under its original", () =>
        {
            var handler = PronunciationFixture(PronunciationJson("[00:01]中文歌词"), Wiki("粤语"));
            Equal("[00:01.000]中文歌词\nroman", Lookup(PronunciationPlugin(handler, "\"EnableCantonesePronunciation\":true")));
            Equal(2, handler.Calls.Count);
        });
        Test("Cantonese pronunciation off returns only original when Japanese pronunciation is enabled", () =>
        {
            var handler = PronunciationFixture(PronunciationJson("[00:01]中文歌词"), Wiki("粤语"));
            Equal("[00:01]中文歌词", Lookup(PronunciationPlugin(handler, "\"JapaneseText\":1")));
        });
        Test("Japanese pronunciation selection excludes translation", () =>
            Equal("[00:01.000]にほんごの歌詞\nroman", Lookup(PronunciationPlugin(
                PronunciationFixture(PronunciationJson(), Wiki("日语")), "\"JapaneseText\":1"))));
        Test("Japanese combined selection orders original pronunciation translation", () =>
            Equal("[00:01.000]にほんごの歌詞\nroman\n译文", Lookup(PronunciationPlugin(
                PronunciationFixture(PronunciationJson(), Wiki("日语")), "\"JapaneseText\":2"))));
        Test("Korean choice is independent from the Japanese choice", () =>
            Equal("[00:01.000]한국어가사\nroman\n译文", Lookup(PronunciationPlugin(
                PronunciationFixture(PronunciationJson("[00:01]한국어가사"), Wiki("韩语")), "\"JapaneseText\":1,\"KoreanText\":2"))));
        Test("Three-line selection falls back to original plus translation when pronunciation is absent", () =>
        {
            var handler = PronunciationFixture(PronunciationJson(roma: null), Wiki("日语"));
            Equal("[00:01.000]にほんごの歌詞\n译文", Lookup(PronunciationPlugin(handler, "\"JapaneseText\":2")));
            Equal(2, handler.Calls.Count);
        });
        Test("Three-line selection falls back to original plus pronunciation when translation is absent", () =>
            Equal("[00:01.000]にほんごの歌詞\nroman", Lookup(PronunciationPlugin(
                PronunciationFixture(PronunciationJson(translation: null), Wiki("日语")), "\"JapaneseText\":2"))));
        Test("Three-line selection returns the untouched original when both extras are absent", () =>
            Equal("[00:01]にほんごの歌詞", Lookup(PronunciationPlugin(
                PronunciationFixture(PronunciationJson(roma: "[offset:0]", translation: "//"), Wiki("日语")), "\"JapaneseText\":2"))));
        Test("Pronunciation-only choice does not silently substitute translation", () =>
            Equal("[00:01]にほんごの歌詞", Lookup(PronunciationPlugin(
                PronunciationFixture(PronunciationJson(roma: null), Wiki("日语")), "\"JapaneseText\":1"))));
        Test("Legacy global-only formats migrate to original-inclusive output without metadata calls", () =>
        {
            foreach (var format in new[] { 0, 2 })
            {
                var handler = PronunciationFixture(PronunciationJson(), Wiki("日语"));
                var plugin = PronunciationPlugin(handler, "\"JapaneseText\":2");
                SetConfig(plugin, "{\"Format\":" + format + ",\"JapaneseText\":2}");
                Equal(format == 0 ? "[00:01]にほんごの歌詞" : "[00:01.000]にほんごの歌詞\n译文", Lookup(plugin));
                Equal(1, handler.Calls.Count);
            }
        });
        Test("Same-line combined pronunciation uses spaced separators", () =>
        {
            var plugin = PronunciationPlugin(PronunciationFixture(PronunciationJson(), Wiki("日语")), "\"JapaneseText\":2");
            SetConfig(plugin, "{\"Format\":1,\"JapaneseText\":2}");
            Equal("[00:01.000]にほんごの歌詞 / roman / 译文", Lookup(plugin));
        });
        Test("Both auxiliary tracks align independently to the original offset", () =>
        {
            Equal("[offset:500]\n[00:01.000]Original\nroman\n译文",
                PronunciationMerge("[offset:500]\n[00:01]Original", "[00:00.500]roman", "[00:01.005]译文", out var used));
            Equal(2, used);
        });
        Test("Per-cue fallback retains each independently supported auxiliary line", () =>
        {
            Equal("[00:01.000]One\nroman\n[00:03.000]Two\n译文",
                PronunciationMerge("[00:01]One\n[00:03]Two", "[00:01]roman", "[00:03]译文", out var used));
            Equal(2, used);
        });
        Test("Fifty-one-ms pronunciation mismatch falls back to the supported translation", () =>
        {
            Equal("[00:01.000]Original\n译文", PronunciationMerge("[00:01]Original", "[00:01.051]roman", "[00:01]译文", out var used));
            Equal(1, used);
        });
        Test("Two failed alignments return the unmodified original", () =>
        {
            Equal("[offset:0]\n[00:01]Original", PronunciationMerge("[offset:0]\n[00:01]Original", "[00:05]roman", "[00:06]译文", out var used));
            Equal(0, used);
        });
        Test("Untimed pronunciation cannot borrow playback cues but timed translation remains usable", () =>
        {
            Equal("[00:01.000]Original\n译文", PronunciationMerge("[00:01]Original", "roman", "[00:01]译文", out var used));
            Equal(1, used);
        });
        Test("Duplicate English text adds no repeated rows or empty placeholders", () =>
        {
            Equal("[00:01]English", PronunciationMerge("[00:01]English", "[00:01]English", "[00:01]English", out var used));
            Equal(0, used);
            Equal("[00:01.000]Original\nSame extra", PronunciationMerge("[00:01]Original", "[00:01]Same extra", "[00:01]Same extra", out used));
            Equal(1, used);
        });
        Test("Untimed three-track pairs preserve markers and spaced slash formatting", () =>
        {
            Equal("[Verse]\nOne / roman1 / 译文1\nTwo / roman2 / 译文2\n\n[Chorus]\nThree / roman3 / 译文3",
                PronunciationMerge("[Verse]\nOne\nTwo\n\n[Chorus]\nThree", "roman1\nroman2\nroman3", "译文1\n译文2\n译文3", out var used));
            Equal(2, used);
        });
        Test("Untimed unequal verses retain original then pronunciation then translation blocks", () =>
        {
            Equal("One\nTwo\n\nroman paragraph\n\n译文1\n译文2", PronunciationMerge("One\nTwo", "roman paragraph", "译文1\n译文2", out var used));
            Equal(2, used);
        });
        Test("An incompatible untimed track cannot prevent the other track from displaying", () =>
        {
            Equal("One / 译文1\n\nTwo / 译文2", PronunciationMerge("One\n\nTwo", "r1\n\nr2\n\nr3", "译文1\n译文2", out var used));
            Equal(1, used);
        });
        Test("Identical untimed auxiliary paragraphs appear once in unequal verse blocks", () =>
        {
            Equal("One\nTwo\n\nSame paragraph", PronunciationMerge("One\nTwo", "Same paragraph", "Same paragraph", out var used));
            Equal(1, used);
        });
        Test("Language metadata parser ignores unrelated UI text and rejects mixed labels", () =>
        {
            Equal(SongLanguage.Cantonese, LanguagePreferences.ParseServiceLanguage((JObject)JObject.Parse(Wiki("粤语"))["data"]));
            Equal(SongLanguage.English, LanguagePreferences.ParseServiceLanguage((JObject)JObject.Parse(Wiki("英语"))["data"]));
            Equal(SongLanguage.Other, LanguagePreferences.ParseServiceLanguage((JObject)JObject.Parse(Wiki("日语", "韩语"))["data"]));
            Equal(SongLanguage.Unknown, LanguagePreferences.ParseServiceLanguage(JObject.Parse("{\"blocks\":[{\"code\":\"OTHER\",\"creatives\":[{\"creativeType\":\"language\",\"uiElement\":{\"textLinks\":[{\"text\":\"粤语\"}]}}]}]}")));
        });
        Test("Malformed language payloads cannot discard usable original lyrics", () =>
        {
            foreach (var bad in new[] { "{\"code\":200}", "{\"code\":-460}", "not json", "{\"code\":200,\"data\":{\"blocks\":[null,1,{\"code\":\"SONG_PLAY_ABOUT_SONG_BASIC\",\"creatives\":[{\"creativeType\":\"language\",\"uiElement\":1}]}]}}" })
                Equal("[00:01.000]にほんごの歌詞\nroman", Lookup(PronunciationPlugin(PronunciationFixture(PronunciationJson(), bad), "\"JapaneseText\":1")));
        });
        Test("Han-only text does not guess Cantonese when the service language is unavailable", () =>
            Equal("[00:01.000]中文歌词\n译文", Lookup(PronunciationPlugin(
                PronunciationFixture(PronunciationJson("[00:01]中文歌词"), "{\"code\":-460}"), "\"EnableCantonesePronunciation\":true"))));
        Test("Explicit other-language metadata overrides a Japanese script guess", () =>
            Equal("[00:01.000]にほんごの歌詞\n译文", Lookup(PronunciationPlugin(
                PronunciationFixture(PronunciationJson(), Wiki("英语")), "\"JapaneseText\":1"))));
        Test("Explicit multilingual metadata retains translation instead of guessing a language", () =>
            Equal("[00:01.000]にほんごの歌詞\n译文", Lookup(PronunciationPlugin(
                PronunciationFixture(PronunciationJson(), Wiki("日语", "韩语")), "\"JapaneseText\":1"))));
        Test("A stalled language request stops within two seconds and preserves usable lyrics", () =>
        {
            var handler = PronunciationFixture(PronunciationJson(), Wiki("日语"));
            handler.StallLanguage = true;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Equal("[00:01.000]にほんごの歌詞\nroman\n译文", Lookup(PronunciationPlugin(handler, "\"JapaneseText\":2")));
            True(watch.Elapsed < TimeSpan.FromSeconds(4));
            Equal(2, handler.Calls.Count);
        });
        Test("Fifty-ms pronunciation alignment is included independently from translation", () =>
        {
            Equal("[00:01.000]Original\nroman\n译文", PronunciationMerge("[00:01]Original", "[00:01.050]roman", "[00:01]译文", out var used));
            Equal(2, used);
        });
        Test("Script fallback distinguishes Japanese Korean and uncertain mixed scripts", () =>
        {
            Equal(SongLanguage.Japanese, LanguagePreferences.InferScriptLanguage("[00:01]こんにちは"));
            Equal(SongLanguage.Korean, LanguagePreferences.InferScriptLanguage("[00:01]한국어가사"));
            Equal(SongLanguage.Unknown, LanguagePreferences.InferScriptLanguage("[00:01]こんにちは 한국어가사"));
            Equal(SongLanguage.Unknown, LanguagePreferences.InferScriptLanguage("[ar:こんにちは]\n[00:01]中文歌词"));
        });
        Test("Known language labels are cached by song ID without caching lyric text", () =>
        {
            var handler = PronunciationFixture(PronunciationJson("[00:01]中文歌词"), Wiki("粤语"));
            var plugin = PronunciationPlugin(handler, "\"EnableCantonesePronunciation\":true");
            Equal("[00:01.000]中文歌词\nroman", Lookup(plugin));
            Equal("[00:01.000]中文歌词\nroman", Lookup(plugin));
            Equal(1, handler.Calls.Count(c => c.Contains("/about/")));
        });
        Test("Optional language metadata has one request per lookup and shares the nine-send lyric cap", () =>
        {
            var handler = PronunciationFixture(PronunciationJson(), Wiki("日语"));
            using (var session = Session(handler))
            {
                Equal(SongLanguage.Japanese, NeteaseApi.RequestLanguage(101, session).Value);
                Equal(null, NeteaseApi.RequestLanguage(102, session));
                for (var i = 0; i < 12; i++) NeteaseApi.RequestLyric(101, session);
            }
            Equal(LookupSession.MaxLyricRequests, handler.Calls.Count);
        });
        Test("Language cache entries never apply to another song ID", () =>
        {
            var preferences = new LanguagePreferences();
            var handler = new Fixture((request, _) => Json(Wiki(request.RequestUri.Query.Contains("101") ? "粤语" : "韩语")));
            var lyrics = JsonConvert.DeserializeObject<LyricResult>(PronunciationJson("[00:01]中文歌词"));
            using (var first = Session(handler)) Equal(SongLanguage.Cantonese, preferences.Resolve(101, lyrics, first));
            using (var second = Session(handler)) Equal(SongLanguage.Korean, preferences.Resolve(102, lyrics, second));
            using (var third = Session(handler)) Equal(SongLanguage.Cantonese, preferences.Resolve(101, lyrics, third));
            Equal(2, handler.Calls.Count);
        });
        Test("A compatible listing can recover both extras using its own original clock", () =>
        {
            var lyricRequests = 0;
            var handler = new Fixture((request, _) =>
            {
                if (request.RequestUri.AbsolutePath.Contains("search")) return Json(SearchJson(TwoListings()));
                if (request.RequestUri.AbsolutePath.Contains("/about/")) return Json(Wiki("日语"));
                return Json(++lyricRequests == 1 ? PronunciationJson(translation: null) :
                    PronunciationJson("[00:02]にほんごの別の歌詞", "[00:02]second roman", "[00:02]第二译文"));
            });
            var plugin = CreatePlugin(handler);
            SetConfig(plugin, "{\"Format\":1,\"TwoLineTranslation\":true,\"JapaneseText\":2}");
            Equal("[00:02.000]にほんごの別の歌詞\nsecond roman\n第二译文", Lookup(plugin, "Singer", "Song", "Album"));
            Equal(4, handler.Calls.Count);
            Equal(1, handler.Calls.Count(c => c.Contains("/about/")));
        });
        Test("A failed compatible listing preserves the first partial pronunciation result", () =>
        {
            var lyricRequests = 0;
            var handler = new Fixture((request, _) =>
            {
                if (request.RequestUri.AbsolutePath.Contains("search")) return Json(SearchJson(TwoListings()));
                if (request.RequestUri.AbsolutePath.Contains("/about/")) return Json(Wiki("日语"));
                return Json(++lyricRequests == 1 ? PronunciationJson(translation: null) : "{\"code\":200}");
            });
            var plugin = CreatePlugin(handler);
            SetConfig(plugin, "{\"Format\":1,\"TwoLineTranslation\":true,\"JapaneseText\":2}");
            Equal("[00:01.000]にほんごの歌詞\nroman", Lookup(plugin, "Singer", "Song", "Album"));
            True(handler.Calls.Count <= LookupSession.MaxSearchRequests + LookupSession.MaxLyricRequests);
        });
        Test("An optional language refusal does not suppress later lyric transport fallback", () =>
        {
            var handler = new Fixture((request, _) =>
            {
                if (request.RequestUri.AbsolutePath.Contains("/about/")) return Json("{\"code\":-460}");
                if (request.RequestUri.Scheme == "https") throw new HttpRequestException("offline");
                return Json(PronunciationJson());
            });
            using (var session = Session(handler))
            {
                Equal(null, NeteaseApi.RequestLanguage(101, session));
                True(NeteaseApi.RequestLyric(101, session) != null);
            }
            Equal(1, handler.Calls.Count(c => c.StartsWith("GET http://")));
        });
        Test("Language options save independently while keeping rollback-safe Format values", () =>
        {
            var plugin = PronunciationPlugin(new Fixture((_, __) => Json(Lyrics)), "\"JapaneseText\":2,\"KoreanText\":1,\"EnableCantonesePronunciation\":true");
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                var choices = panel.Controls.OfType<ComboBox>().Where(c => c.Name == "JapaneseContent" || c.Name == "KoreanContent").OrderBy(c => c.Top).ToArray();
                Equal(4, choices[0].SelectedIndex); Equal(3, choices[1].SelectedIndex);
                Equal("Lyrics + Pronunciation + Translation", choices[0].Items[4].ToString());
                plugin.SaveSettings();
                panel.Controls.OfType<ComboBox>().Single(c => c.Name == "OutputLayout").SelectedIndex = 0;
                True(choices.All(c => c.Enabled)); // Layout does not disable content overrides.
            }
            var saved = JObject.Parse(File.ReadAllText(Path.Combine(_directories[plugin], "netease_config")));
            Equal(1, (int)saved["Format"]); Equal(true, (bool)saved["TwoLineTranslation"]);
            Equal(2, (int)saved["JapaneseText"]); Equal(1, (int)saved["KoreanText"]); Equal(true, (bool)saved["EnableCantonesePronunciation"]);
        });
        Test("Invalid language choice numbers reset only that choice", () =>
        {
            var plugin = PronunciationPlugin(new Fixture((_, __) => Json(Lyrics)), "\"JapaneseText\":99,\"KoreanText\":-1,\"AllowInsecureHttp\":false");
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                True(panel.Controls.OfType<ComboBox>().Where(c => c.Name == "JapaneseContent" || c.Name == "KoreanContent").All(c => c.SelectedIndex == 0));
                Equal(false, panel.Controls.OfType<CheckBox>().Single(c => c.Text.StartsWith("Allow HTTP")).Checked);
            }
        });
        Test("Malformed new preference types cannot reset old format or HTTP opt-out", () =>
        {
            foreach (var fields in new[] { "\"JapaneseText\":\"bad choice\",\"KoreanText\":{},\"EnableCantonesePronunciation\":[]",
                "\"JapaneseText\":99999999999999999999,\"KoreanText\":true,\"EnableCantonesePronunciation\":\"bad bool\"" })
            {
                var plugin = PronunciationPlugin(new Fixture((_, __) => Json(Lyrics)), fields + ",\"AllowInsecureHttp\":false");
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    Equal(0, panel.Controls.OfType<ComboBox>().Single(c => c.Name == "OutputLayout").SelectedIndex);
                    True(panel.Controls.OfType<ComboBox>().Where(c => c.Name == "JapaneseContent" || c.Name == "KoreanContent").All(c => c.SelectedIndex == 0));
                    Equal(false, panel.Controls.OfType<CheckBox>().Single(c => c.Text.StartsWith("Allow HTTP")).Checked);
                    Equal(0, panel.Controls.OfType<ComboBox>().Single(c => c.Name == "CantoneseContent").SelectedIndex);
                }
            }
        });
        ContentChoiceTests();
        PronunciationRecordingTests();
    }

    private static void PronunciationRecordingTests()
    {
        var assembly = typeof(Program).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("PronunciationRecordings.json"));
        JArray recordings;
        using (var reader = new StreamReader(assembly.GetManifestResourceStream(resource))) recordings = JArray.Parse(reader.ReadToEnd());
        foreach (var recording in recordings.OfType<JObject>())
        {
            var captured = recording;
            Test("Captured " + (string)captured["language"] + " pronunciation timing and language metadata replay", () =>
            {
                var original = (string)captured["original"];
                var roman = (string)captured["pronunciation"];
                var translation = (string)captured["translation"];
                var expectedLanguage = (SongLanguage)Enum.Parse(typeof(SongLanguage), (string)captured["language"]);
                Equal(expectedLanguage, LanguagePreferences.ParseServiceLanguage((JObject)captured["wiki"]["data"]));
                var merged = PronunciationMerge(original, roman, expectedLanguage == SongLanguage.Cantonese ? null : translation, out var used);
                Equal(expectedLanguage == SongLanguage.Cantonese ? 1 : 2, used);
                var outputLines = merged.Split('\n');
                var romanTexts = roman.Split('\n').Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"^\[[^\]]+\]", "")).Where(l => l.StartsWith("roman")).ToArray();
                True(romanTexts.Length > 10);
                Equal(romanTexts.Length, romanTexts.Count(t => outputLines.Contains(t)));
                if (expectedLanguage != SongLanguage.Cantonese)
                {
                    var translations = translation.Split('\n').Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"^\[[^\]]+\]", "")).Where(l => l.StartsWith("译文")).ToArray();
                    True(translations.Length > 10);
                    Equal(translations.Length, translations.Count(t => outputLines.Contains(t)));
                }
                var handler = PronunciationFixture(PronunciationJson(original, roman, translation), captured["wiki"].ToString());
                var plugin = PronunciationPlugin(handler, "\"EnableCantonesePronunciation\":true,\"JapaneseText\":2,\"KoreanText\":2");
                Equal(merged, Lookup(plugin)); Equal(2, handler.Calls.Count);
            });
        }
    }

    private static int LivePronunciation()
    {
        var failures = 0;
        foreach (var id in new long[] { 409931770, 1409311773, 2676664123, 400875204 })
        {
            using (var session = new LookupSession(Console.WriteLine))
            {
                var lyrics = NeteaseApi.RequestLyric(id, session);
                if (!LyricProcessor.HasLyricText(lyrics?.lrc?.lyric))
                {
                    Console.WriteLine("LIVE ID " + id + ": original unavailable"); failures++; continue;
                }
                var language = new LanguagePreferences().Resolve(id, lyrics, session);
                var combined = LyricProcessor.MergePronunciation(lyrics.lrc.lyric, lyrics.romalrc?.lyric,
                    id == 409931770 ? null : lyrics.tlyric?.lyric, true, session.Report, out var used);
                var expected = id == 409931770 || id == 400875204 ? 1 : 2;
                var cueSizes = combined.Split('\n').Aggregate(new System.Collections.Generic.List<int>(), (sizes, line) =>
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, @"^\[[0-9]+:")) sizes.Add(1);
                    else if (sizes.Count > 0 && !string.IsNullOrWhiteSpace(line)) sizes[sizes.Count - 1]++;
                    return sizes;
                });
                var groups = cueSizes.Count(s => s == expected + 1);
                Console.WriteLine("LIVE ID " + id + ": language=" + language + ", extras=" + used +
                    ", " + (expected + 1) + "-line cues=" + groups + ", original-only=" + (combined == lyrics.lrc.lyric));
                if (used != expected || groups == 0) failures++;
            }
        }
        return failures == 0 ? 0 : 1;
    }
}
