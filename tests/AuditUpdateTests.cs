using MusicBeePlugin;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

internal static partial class Program
{
    private static void AuditUpdateTests()
    {
        Test("Loading 1.9.4 settings immediately repairs rollback flags without Save", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            const string old = "{\"DisplaySettingsVersion\":1,\"ShowTranslation\":true,\"ShowPronunciation\":false,\"SeparateLines\":true,\"JapaneseOverride\":3,\"Fuzzy\":true,\"UseLegacyMatch\":true,\"AllowInsecureHttp\":false}";
            SetConfig(plugin, old);
            var path = Path.Combine(_directories[plugin], "netease_config");
            var saved = JObject.Parse(File.ReadAllText(path));
            Equal(false, (bool)saved["Fuzzy"]); Equal(false, (bool)saved["UseLegacyMatch"]);
            Equal(true, (bool)saved["AllowSmallSpellingDifferences"]);
            Equal(false, (bool)saved["AllowInsecureHttp"]); Equal(3, (int)saved["JapaneseOverride"]);
            Equal(old, File.ReadAllText(path + ".pre-spelling.bak"));
            var repaired = File.ReadAllBytes(path); plugin.ReadConfig();
            True(repaired.SequenceEqual(File.ReadAllBytes(path)));
            Equal(old, File.ReadAllText(path + ".pre-spelling.bak"));
        });
        Test("Spelling migration preserves every old effective choice and rejects malformed new fields independently", () =>
        {
            foreach (var legacy in new[] { false, true }) foreach (var fuzzy in new[] { false, true })
            {
                var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
                SetConfig(plugin, "{\"UseLegacyMatch\":" + legacy.ToString().ToLowerInvariant() + ",\"Fuzzy\":" + fuzzy.ToString().ToLowerInvariant() +
                    ",\"AllowSmallSpellingDifferences\":[],\"AllowInsecureHttp\":false}");
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle); Equal(!legacy || fuzzy, DisplayCheck(panel, "SmallSpellingDifferences").Checked);
                    True(!DisplayCheck(panel, "AllowHttpFallback").Checked);
                }
            }
        });
        Test("A failed load-time repair retains preferences and the original file", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            var path = Path.Combine(_directories[plugin], "netease_config");
            Directory.CreateDirectory(path + ".pre-spelling.bak");
            const string old = "{\"UseLegacyMatch\":true,\"Fuzzy\":false,\"AllowInsecureHttp\":false}";
            SetConfig(plugin, old); Equal(old, File.ReadAllText(path));
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle); True(!DisplayCheck(panel, "SmallSpellingDifferences").Checked);
                True(!DisplayCheck(panel, "AllowHttpFallback").Checked);
            }
        });
        Test("Changing UI language does not change live lookups or persisted settings until Save", () =>
        {
            var handler = PronunciationFixture(PronunciationJson(), Wiki("日语"));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
            SetConfig(plugin, ModernSettings());
            var path = Path.Combine(_directories[plugin], "netease_config"); var saved = File.ReadAllBytes(path);
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                DisplayCheck(panel, "ShowTranslation").Checked = false;
                DisplayCheck(panel, "ShowPronunciation").Checked = true;
                DisplayCheck(panel, "AllowHttpFallback").Checked = false;
                DisplayCheck(panel, "SmallSpellingDifferences").Checked = false;
                DisplayCombo(panel, "OutputLayout").SelectedIndex = 1;
                DisplayCombo(panel, "InterfaceLanguage").SelectedIndex = 2;
                Equal("[00:01.000]にほんごの歌詞\n译文", Lookup(plugin));
                True(saved.SequenceEqual(File.ReadAllBytes(path)));
                DisplayCombo(panel, "InterfaceLanguage").SelectedIndex = 1;
                True(!DisplayCheck(panel, "ShowTranslation").Checked); True(DisplayCheck(panel, "ShowPronunciation").Checked);
            } // Cancel: host closes/disposes the panel without invoking SaveSettings.
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle); True(DisplayCheck(panel, "ShowTranslation").Checked);
                True(!DisplayCheck(panel, "ShowPronunciation").Checked); True(DisplayCheck(panel, "AllowHttpFallback").Checked);
                True(DisplayCheck(panel, "SmallSpellingDifferences").Checked); Equal(0, DisplayCombo(panel, "OutputLayout").SelectedIndex);
            }
        });
        Test("Saving a language-rebuilt settings draft applies all choices together", () =>
        {
            var plugin = CreatePlugin(PronunciationFixture(PronunciationJson(), Wiki("日语")),
                (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle); DisplayCheck(panel, "ShowTranslation").Checked = false;
                DisplayCheck(panel, "ShowPronunciation").Checked = true;
                DisplayCombo(panel, "InterfaceLanguage").SelectedIndex = 2; plugin.SaveSettings();
            }
            Equal("[00:01.000]にほんごの歌詞\nroman", Lookup(plugin));
        });
        Test("Missing extras use at most two listings without extra language requests", () =>
        {
            var listings = Enumerable.Range(1, 3).Select(id => Song(id, "晴天", "周杰伦", "叶惠美", 269000)).ToArray();
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? SearchJson(listings) :
                "{\"code\":200,\"lrc\":{\"lyric\":\"[00:01]故事的小黄花\"}}"));
            True(Lookup(CreatePlugin(handler), "周杰伦", "晴天", "叶惠美") != null);
            Equal(2, handler.Calls.Count(c => c.Contains("/lyric"))); True(handler.Calls.All(c => !c.Contains("/about/")));
        });
        Test("Missing originals keep all three compatible attempts", () =>
        {
            var listings = Enumerable.Range(1, 3).Select(id => Song(id, "Song", "Singer", "Album", 200000)).ToArray();
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? SearchJson(listings) :
                request.RequestUri.Query.Contains("id=3&") ? Lyrics : "{\"code\":200}"));
            var plugin = CreatePlugin(handler); SetConfig(plugin, "{\"Format\":0}");
            Equal("[00:01.00]Hello", Lookup(plugin, "Singer", "Song", "Album"));
            Equal(6, handler.Calls.Count(c => c.Contains("/lyric")));
        });
        Test("An instrumental first listing can recover a vocal equivalent", () =>
        {
            var count = 0;
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? SearchJson(TwoListings()) :
                ++count == 1 ? "{\"code\":200,\"nolyric\":true}" : Lyrics));
            var plugin = CreatePlugin(handler); SetConfig(plugin, "{\"Format\":0}");
            Equal("[00:01.00]Hello", Lookup(plugin, "Singer", "Song", "Album")); Equal(2, count);
            var overrideHandler = new Fixture((_, __) => Json("{\"code\":200,\"nolyric\":true}"));
            Equal(null, Lookup(CreatePlugin(overrideHandler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=1" : null)));
            Equal(1, overrideHandler.Calls.Count);
        });
        Test("Already cached Mandarin metadata avoids translation-only retries", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("/about/") ? Wiki("普通话") :
                request.RequestUri.AbsolutePath.Contains("search") ? SearchJson(TwoListings()) : Lyrics));
            var plugin = CreatePlugin(handler);
            var languages = (LanguagePreferences)typeof(Plugin).GetField("_languagePreferences", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(plugin);
            using (var session = Session(handler)) Equal(SongLanguage.Mandarin, languages.Resolve(1,
                Newtonsoft.Json.JsonConvert.DeserializeObject<LyricResult>(PronunciationJson()), session));
            handler.Calls.Clear(); Equal("[00:01.00]Hello", Lookup(plugin, "Singer", "Song", "Album"));
            Equal(1, handler.Calls.Count(c => c.Contains("/lyric"))); True(handler.Calls.All(c => !c.Contains("/about/")));
        });
        Test("5xx permits configured recovery while 4xx anti-bot and redirects still block HTTP", () =>
        {
            foreach (var status in new[] { 502, 503, 504, 403, 429, 302 })
            {
                var handler = new Fixture((request, _) =>
                {
                    if (request.Method == HttpMethod.Post) return new HttpResponseMessage((HttpStatusCode)status);
                    if (request.RequestUri.Scheme == "https") throw new HttpRequestException("blocked");
                    return Json(request.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics);
                });
                var result = Lookup(CreatePlugin(handler), "Singer", "Song", "Album");
                Equal(status >= 500, result != null); Equal(status >= 500, handler.Calls.Any(c => c.Contains(" http://")));
            }
        });
        Test("5xx API responses recover without bypassing the HTTP opt-out", () =>
        {
            foreach (var allow in new[] { true, false })
            {
                var handler = new Fixture((request, _) => Json(request.RequestUri.Scheme == "https" ? "{\"code\":502}" : Lyrics));
                var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=1" : null);
                SetConfig(plugin, "{\"AllowInsecureHttp\":" + allow.ToString().ToLowerInvariant() + "}");
                Equal(allow, Lookup(plugin) != null); Equal(allow ? 3 : 2, handler.Calls.Count);
            }
        });
        Test("Contractions and dotted acronyms do not introduce spurious Roman numbers", () =>
        {
            Equal(1L, Select(new[] { Song(1, "Im Yours", "Singer", "Album", 200000) }, "I'm Yours"));
            Equal(1L, Select(new[] { Song(1, "DC Love", "Singer", "Album", 200000) }, "D.C. Love"));
            Equal(0L, Select(new[] { Song(1, "Rocky III", "Singer", "Album", 200000) }, "Rocky II"));
            Equal(0L, Select(new[] { Song(1, "Theme Part 2", "Singer", "Album", 200000) }, "Theme Part I"));
            Equal(0L, Select(new[] { Song(1, "Theme II", "Singer", "Album", 200000) }, "Theme I"));
        });
        Test("Additional remaster spellings remain notes rather than numeric identity", () =>
        {
            foreach (var suffix in new[] { " (2009 Remastered Version)", " (Remastered Version)", " (2009 Digital Remaster)", " [2009 - Remaster]" })
                Equal(1L, Select(new[] { Song(1, "Yesterday", "Singer", "Album", 200000) }, "Yesterday" + suffix));
        });
        Test("Only the file's matching track tag authorizes leading-number removal", () =>
        {
            var song = Song(1, "Lemon", "Singer", "Album", 200000);
            foreach (var title in new[] { "01. Lemon", "01 - Lemon" })
            {
                Equal(1L, SearchMatch.SelectCandidate(new[] { song }, title, "Singer", "Album", 200000, trackNumber: "1/12"));
                foreach (var tag in new[] { null, "2", "bad", "0" })
                    Equal(0L, SearchMatch.SelectCandidate(new[] { song }, title, "Singer", "Album", 200000, trackNumber: tag));
            }
            Equal("1.5 Years", SearchMatch.RemoveTaggedTrackNumber("1.5 Years", "1"));
        });
        Test("The host callback uses the requested file's track-number tag", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ?
                SearchJson(new[] { Song(1, "Lemon", "Singer", "Album", 200000) }) : Lyrics));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.TrackTitle ? "01. Lemon" :
                tag == Plugin.MetaDataType.TrackNo ? "1" : null, duration: "3:20");
            Equal("[00:01.00]Hello", Lookup(plugin, "Singer", "ignored", "Album"));
        });
        Test("Soundtrack aliases require the same named work in both albums plus artist and duration guards", () =>
        {
            var song = Song(1, "Let It Go", "Idina Menzel", "Frozen (Original Motion Picture Soundtrack)", 224000);
            Equal(1L, SearchMatch.SelectCandidate(new[] { song }, "Let It Go (From \"Frozen\")", "Idina Menzel", "Frozen OST", 224000));
            foreach (var album in new[] { "Other OST", null })
                Equal(0L, SearchMatch.SelectCandidate(new[] { song }, "Let It Go (From \"Frozen\")", "Idina Menzel", album, 224000));
            Equal(0L, SearchMatch.SelectCandidate(new[] { song }, "Let It Go (From \"Frozen\")", "Other Singer", "Frozen OST", 224000));
            Equal(0L, SearchMatch.SelectCandidate(new[] { song }, "Let It Go (From \"Frozen\")", "Idina Menzel", "Frozen OST", null));
        });
        Test("Broken original timestamps remain untimed while valid cues keep their translation", () =>
        {
            const string original = "[00:01]one\n[00:02.1x]two\n[00:03]three";
            foreach (var separate in new[] { true, false })
            {
                var separator = separate ? "\n" : " / ";
                Equal("[00:01.000]one" + separator + "1\ntwo\n[00:03.000]three" + separator + "3",
                    LyricProcessor.MergePronunciation(original, null, "[00:01]1\n[00:03]3", separate, null, out var tracks));
                Equal(1, tracks);
            }
        });
        Test("Uninstall removes only plugin-owned backups and GUID temporary files", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            var path = Path.Combine(_directories[plugin], "netease_config");
            foreach (var suffix in new[] { "", ".pre9.bak", ".pre-display.bak", ".pre-spelling.bak", "." + Guid.NewGuid().ToString("N") + ".tmp" })
                File.WriteAllText(path + suffix, "test");
            File.WriteAllText(path + ".user.tmp", "keep"); File.WriteAllText(path + ".unrelated.bak", "keep");
            plugin.Uninstall();
            True(!File.Exists(path)); True(File.Exists(path + ".user.tmp")); True(File.Exists(path + ".unrelated.bak"));
            Equal(2, Directory.GetFiles(_directories[plugin]).Length);
        });
    }
}
