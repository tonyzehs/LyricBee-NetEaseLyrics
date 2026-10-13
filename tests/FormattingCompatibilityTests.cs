using MusicBeePlugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static partial class Program
{
    private static void FormattingCompatibilityTests()
    {
        Test("Small rounding errors align within the inclusive fifty-ms limit", () =>
        {
            foreach (var fraction in new[] { "001", "005", "050" })
                Equal("[00:01.000]One\nFirst\n[00:03.000]Two\nSecond",
                    LyricProcessor.InjectTranslationTwoLines("[00:01]One\n[00:03]Two", "[00:01." + fraction + "]First\n[00:03." + fraction + "]Second"));
            Equal("[00:15.975]Original\nTranslation", LyricProcessor.InjectTranslationTwoLines("[00:15.975]Original", "[00:15.98]Translation"));
        });
        Test("Fifty-one-ms errors cannot borrow translation", () =>
            Equal("[00:01.000]One\n[00:03.000]Two",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\n[00:03]Two", "[00:01.051]First\n[00:03.051]Second")));
        Test("Exact matches have priority over a nearby faster cue", () =>
            Equal("[00:01.000]One\n[00:01.040]Two\nTranslation",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\n[00:01.040]Two", "[00:01.040]Translation")));
        Test("Equidistant cues remain unpaired", () =>
            Equal("[00:01.000]One\n[00:01.040]Two",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\n[00:01.040]Two", "[00:01.020]Translation")));
        Test("A near duplicate of an exact translation cannot spill onto the next fast cue", () =>
            Equal("[00:01.000]One\nExact\n[00:01.040]Two",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\n[00:01.040]Two", "[00:01]Exact\n[00:01.001]Near duplicate")));
        Test("A translation is used only once and the closest competing line wins", () =>
            Equal("[00:01.000]One\nNearest\n[00:03.000]Two\nExact\n[00:05.000]Three",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\n[00:03]Two\n[00:05]Three",
                    "[00:01.010]Nearest\n[00:01.020]Unused\n[00:03]Exact")));
        Test("A few coincidental near stamps do not establish alignment", () =>
        {
            Equal("[00:01.000]One\n[00:03.000]Two\n[00:05.000]Three",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\n[00:03]Two\n[00:05]Three", "[00:01.010]Near\n[00:13]Far\n[00:15]Other"));
            Equal("[00:01.000]One\nExact\n[00:03.000]Two\n[00:05.000]Three",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\n[00:03]Two\n[00:05]Three", "[00:01]Exact\n[00:13]Far\n[00:15]Other"));
        });
        Test("Offset-only original can select raw times without losing playback correction", () =>
            Equal("[offset:500]\n[00:01.000]One\nFirst\n[00:03.000]Two\nSecond",
                LyricProcessor.InjectTranslationTwoLines("[offset:500]\n[00:01]One\n[00:03]Two", "[00:01]First\n[00:03]Second")));
        Test("Adjusted timing wins when its sequence fits better", () =>
            Equal("[offset:500]\n[00:01.000]One\nFirst\n[00:03.000]Two\nSecond",
                LyricProcessor.InjectTranslationTwoLines("[offset:500]\n[00:01]One\n[00:03]Two", "[00:00.500]First\n[00:02.500]Second")));
        Test("Equal-quality conflicting clocks do not guess between shifted pairs", () =>
            Equal("[offset:1000]\n[00:01.000]One\n[00:02.000]Two\n[00:03.000]Three",
                LyricProcessor.InjectTranslationTwoLines("[offset:1000]\n[00:01]One\n[00:02]Two\n[00:03]Three", "[00:01]First\n[00:02]Second")));
        Test("Alignment traces disclose the selected rule and error bound", () =>
        {
            var trace = new List<string>();
            LyricProcessor.MergeTranslation("[offset:500]\n[00:01]Private text", "[00:01.005]Translation", true, trace.Add);
            True(trace.Any(t => t.Contains("raw") && t.Contains("5 ms") && t.Contains("limit 50")));
            True(trace.All(t => !t.Contains("Private text") && !t.Contains("Translation")));
        });
        Test("Long lyric documents align without a quadratic scan", () =>
        {
            var original = string.Join("\n", Enumerable.Range(1, 2000).Select(i => "[" + (i * 3 / 60).ToString("00") + ":" +
                (i * 3 % 60).ToString("00") + ".000]Original " + i));
            var translation = original.Replace(".000]Original", ".005]Translated");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var result = LyricProcessor.InjectTranslationTwoLines(original, translation);
            Equal(2000, result.Split('\n').Count(l => l.StartsWith("Translated")));
            True(clock.Elapsed < TimeSpan.FromSeconds(5));
        });
        Test("Same-line uses shared numeric parsing alignment ordering and metadata", () =>
            Equal("[offset:500]\n[00:01.000]One/First\n[00:10.000]Two/Second",
                LyricProcessor.InjectTranslation("[offset:500]\n[00:10]Two\n[00:01.00]One", "[00:01.001]First\n[00:10.005]Second")));
        Test("Blank translation cannot add a slash in the old selectable format", () =>
            Equal("[00:01.000]One\n[00:02.000]Two/Second",
                LyricProcessor.InjectTranslation("[00:01]One\n[00:02]Two", "[00:01] \n[00:02]Second")));
        Test("One-digit seconds and excess fractional precision are normalized", () =>
        {
            Equal("[00:01.000]One\nFirst", LyricProcessor.InjectTranslationTwoLines("[00:1.00]One", "[00:01]First"));
            Equal("[00:01.000]One\nFirst\n[00:02.123]Two\nSecond",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\n[00:02.1234]Two", "[00:01]First\n[00:02.123]Second"));
        });
        Test("Indented timestamps and offsets still align", () =>
            Equal("[offset:500]\n[00:01.000]One\nFirst", LyricProcessor.InjectTranslationTwoLines(
                "  [offset:500]\n  [00:01]One", "  [00:00.500]First")));
        Test("Invalid original seconds remain in position without blocking valid pairs", () =>
            Equal("[00:01.000]One\nFirst\nBad line\n[00:03.000]Three\nThird",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\n[00:70]Bad line\n[00:03]Three", "[00:01]First\n[00:03]Third")));
        Test("Recognized untimed credits remain outside original translation pairs", () =>
            Equal("作词: Author\n[00:01.000]One\nFirst\n[00:03.000]Three\nThird",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\n作词: Author\n[00:03]Three", "[00:01]First\n[00:03]Third")));
        Test("Untimed section markers do not count as lyric lines", () =>
        {
            Equal("[Verse]\nOne / First\nTwo / Second",
                LyricProcessor.InjectTranslationTwoLines("[Verse]\nOne\nTwo", "First\nSecond"));
            Equal("  [Verse]\nOne / First\nTwo / Second",
                LyricProcessor.InjectTranslationTwoLines("  [Verse]\nOne\nTwo", "First\nSecond"));
        });
        Test("Unequal untimed single verses retain adjacent blocks without flattening", () =>
            Equal("[Verse]\nOne\nTwo\n\nFirst\nSecond\nThird",
                LyricProcessor.InjectTranslationTwoLines("[Verse]\nOne\nTwo", "First\nSecond\nThird")));
        Test("Shared same-line mode also pairs untimed text with spaced separators", () =>
            Equal("One / First\nTwo / Second", LyricProcessor.InjectTranslation("One\nTwo", "First\nSecond")));
        Test("Metadata and placeholders alone are not lyric text", () =>
        {
            foreach (var text in new[] { "[by:translator]", "[offset:500]\n[00:01]", "[Verse]", "//", "作词: Someone" })
                True(!LyricProcessor.HasLyricText(text));
            True(LyricProcessor.HasLyricText("[00:01]日本語\n中文"));
        });
        Test("Translation-only metadata falls back to the usable original", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":200,\"lrc\":{\"lyric\":\"[00:01]Original\"},\"tlyric\":{\"lyric\":\"[by:translator]\"}}"));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
            SetConfig(plugin, "{\"Format\":2}");
            Equal("[00:01]Original", Lookup(plugin));
        });
        Test("Metadata-only original is not accepted as a lyric download", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":200,\"lrc\":{\"lyric\":\"[by:someone]\"}}"));
            Equal(null, Lookup(CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
            Equal(2, handler.Calls.Count);
        });
        Test("Two-line settings save an upstream-safe format and separate preference", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            using (var panel = new Panel()) { plugin.Configure(panel.Handle); plugin.SaveSettings(); }
            var json = JObject.Parse(File.ReadAllText(Path.Combine(_directories[plugin], "netease_config")));
            Equal(1, (int)json["Format"]); Equal(true, (bool)json["TwoLineTranslation"]);
            plugin.ReadConfig();
            using (var panel = new Panel()) { plugin.Configure(panel.Handle); Equal(0, SettingsControls(panel).OfType<ComboBox>().Single(c => c.Name == "OutputLayout").SelectedIndex); }
        });
        Test("Old Format three migrates once with a byte-preserving backup", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            var old = "{\"Format\":3,\"AllowInsecureHttp\":false,\"Fuzzy\":true}";
            SetConfig(plugin, old);
            var path = Path.Combine(_directories[plugin], "netease_config");
            Equal(old, File.ReadAllText(path + ".pre9.bak"));
            var json = JObject.Parse(File.ReadAllText(path));
            Equal(1, (int)json["Format"]); Equal(false, (bool)json["AllowInsecureHttp"]);
            plugin.ReadConfig(); Equal(old, File.ReadAllText(path + ".pre9.bak"));
        });
        Test("Migration failure retains loaded preferences and HTTP opt-out", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            var path = Path.Combine(_directories[plugin], "netease_config");
            Directory.CreateDirectory(path + ".pre9.bak");
            SetConfig(plugin, "{\"Format\":3,\"AllowInsecureHttp\":false}");
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle); Equal(0, SettingsControls(panel).OfType<ComboBox>().Single(c => c.Name == "OutputLayout").SelectedIndex);
                Equal(false, SettingsControls(panel).OfType<CheckBox>().Single(c => c.Text.StartsWith("Allow HTTP")).Checked);
            }
            Equal(3, (int)JObject.Parse(File.ReadAllText(path))["Format"]);
        });
        Test("Separate two-line preference is order-independent and does not override original-only", () =>
        {
            Equal(NeteaseConfig.OutputFormat.BothTwoLines, JsonConvert.DeserializeObject<NeteaseConfig>("{\"TwoLineTranslation\":true,\"Format\":1}").Format);
            Equal(NeteaseConfig.OutputFormat.Original, JsonConvert.DeserializeObject<NeteaseConfig>("{\"TwoLineTranslation\":true,\"Format\":0}").Format);
        });
        Test("Choosing another format clears the persisted two-line preference", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, "{\"Format\":1,\"TwoLineTranslation\":true}");
            using (var panel = new Panel()) { plugin.Configure(panel.Handle); SettingsControls(panel).OfType<ComboBox>().Single(c => c.Name == "OutputLayout").SelectedIndex = 1; plugin.SaveSettings(); }
            var json = JObject.Parse(File.ReadAllText(Path.Combine(_directories[plugin], "netease_config")));
            Equal(1, (int)json["Format"]); Equal(false, (bool)json["TwoLineTranslation"]);
        });
        Test("HTTPS API refusal never causes an HTTP search or lyric request", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":-460}"));
            Equal(null, Lookup(CreatePlugin(handler), "Singer", "Song", "Album"));
            Equal(6, handler.Calls.Count); True(handler.Calls.All(c => c.Contains(" https://")));
            var lyrics = new Fixture((_, __) => Json("{\"code\":-460}"));
            Equal(null, Lookup(CreatePlugin(lyrics, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
            Equal(2, lyrics.Calls.Count); True(lyrics.Calls.All(c => c.Contains(" https://")));
        });
        Test("Malformed or empty secure responses do not trigger an HTTP downgrade", () =>
        {
            foreach (var text in new[] { "invalid", "{\"code\":200}", "null", "{\"code\":200,\"lrc\":{\"lyric\":\"\"}}" })
            {
                var handler = new Fixture((_, __) => Json(text));
                using (var session = Session(handler)) Equal(null, NeteaseApi.RequestLyric(1, session));
                Equal(2, handler.Calls.Count); True(handler.Calls.All(c => c.Contains(" https://")));
            }
        });
        Test("Secure HTTP refusals including rate limits do not downgrade", () =>
        {
            foreach (var code in new[] { 403, 429, 302 })
            {
                var handler = new Fixture((_, __) => new HttpResponseMessage((HttpStatusCode)code));
                using (var session = Session(handler)) Equal(null, NeteaseApi.RequestLyric(1, session));
                Equal(2, handler.Calls.Count); True(handler.Calls.All(c => c.Contains(" https://")));
            }
        });
        Test("Refused new HTTPS plus failed legacy transport still does not expose metadata", () =>
        {
            var handler = new Fixture((request, _) => request.Method == HttpMethod.Post ? Json("{\"code\":-460}") :
                throw new HttpRequestException("legacy transport unavailable"));
            using (var session = Session(handler)) Equal(null, NeteaseApi.RequestLyric(1, session));
            Equal(2, handler.Calls.Count); True(handler.Calls.All(c => c.Contains(" https://")));
        });
        Test("Transport failures retain HTTP recovery and the opt-out", () =>
        {
            foreach (var allow in new[] { true, false })
            {
                var handler = new Fixture((request, _) => request.RequestUri.Scheme == "https" ?
                    throw new HttpRequestException("TLS unavailable") : Json(Lyrics));
                using (var session = Session(handler))
                {
                    session.AllowInsecureHttp = allow;
                    Equal(allow, NeteaseApi.RequestLyric(1, session) != null);
                }
                Equal(allow ? 3 : 2, handler.Calls.Count);
            }
        });
        Test("Timed-out secure interfaces are skipped for the rest of the lookup", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics)) { StallHttps = true };
            using (var session = new LookupSession(null, handler, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(800)))
            {
                Equal(101L, NeteaseApi.Search("Song", session).Single().id);
                Equal("[00:01.00]Hello", NeteaseApi.RequestLyric(101, session).lrc.lyric);
            }
            Equal(2, handler.Calls.Count(c => c.Contains(" https://")));
            Equal(2, handler.Calls.Count(c => c.Contains(" http://")));
        });
        Test("A timeout on the last permitted search still disables the interface for lyrics", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics)) { StallNew = true };
            using (var session = new LookupSession(null, handler, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(900)))
            {
                for (var i = 0; i < 8; i++) session.Request<ApiResponse>("warmup/search", HttpMethod.Get,
                    "https://music.163.com/api/search/get", null, r => r.code == 200, RequestInterface.Other, true);
                Equal(0, NeteaseApi.Search("Song", session).Count());
                Equal("[00:01.00]Hello", NeteaseApi.RequestLyric(101, session).lrc.lyric);
            }
            Equal(1, handler.Calls.Count(c => c.StartsWith("POST ")));
        });
    }
}
