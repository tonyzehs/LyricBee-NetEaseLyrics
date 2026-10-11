using MusicBeePlugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    private const string Lyrics = "{\"code\":200,\"lrc\":{\"lyric\":\"[00:01.00]Hello\"}}";
    private const string Search = "{\"code\":200,\"result\":{\"songCount\":1,\"songs\":[{\"id\":101,\"name\":\"Song\",\"duration\":200000,\"artists\":[{\"name\":\"Singer\"}],\"album\":{\"name\":\"Album\"}}]}}";
    private static int _passed, _failed;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--live")) return LiveSmoke();
        if ((args.Contains("--expect-x64") && !Environment.Is64BitProcess) ||
            (args.Contains("--expect-x86") && Environment.Is64BitProcess))
        {
            Console.WriteLine("FAIL test process architecture does not match the requested architecture");
            return 1;
        }
        foreach (var response in new[] { "{\"code\":-460}", "null", "<html>Blocked</html>", "{\"code\":200}",
            "{\"code\":200,\"result\":{}}", "{\"code\":200,\"result\":{\"songCount\":1}}" })
        {
            var fixture = response;
            Test("Rejected/malformed search: " + fixture, () =>
            {
                var handler = new Fixture((_, __) => Json(fixture));
                using (var session = Session(handler)) Equal(0, NeteaseApi.Search("Song", session).Count());
                Equal(2, handler.Calls.Count);
            });
        }
        Test("A malformed new search falls back to a valid legacy search", () =>
        {
            var handler = new Fixture((_, number) => Json(number == 1 ? "{\"code\":200}" : Search));
            using (var session = Session(handler)) Equal(101L, NeteaseApi.Search("Song", session).Single().id);
            Equal(2, handler.Calls.Count);
            True(handler.Calls[0].StartsWith("POST https://") && handler.Calls[1].StartsWith("GET https://"));
        });
        Test("A valid empty search makes no fallback request", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":200,\"result\":{\"songCount\":0}}"));
            using (var session = Session(handler)) Equal(0, NeteaseApi.Search("Song", session).Count());
            Equal(1, handler.Calls.Count);
        });
        foreach (var response in new[] { "{\"code\":-460}", "null", "invalid json", "{\"code\":200}",
            "{\"code\":200,\"lrc\":{\"lyric\":\"  \"}}", "{\"code\":200,\"uncollected\":true}" })
        {
            var fixture = response;
            Test("Missing/refused new lyrics fall back: " + fixture, () =>
            {
                var handler = new Fixture((_, number) => Json(number == 1 ? fixture : Lyrics));
                using (var session = Session(handler)) Equal("[00:01.00]Hello", NeteaseApi.RequestLyric(101, session).lrc.lyric);
                Equal(2, handler.Calls.Count);
            });
        }
        Test("A declared instrumental makes no fallback request", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":200,\"nolyric\":true}"));
            using (var session = Session(handler)) True(NeteaseApi.RequestLyric(101, session).nolyric);
            Equal(1, handler.Calls.Count);
        });
        Test("HTTP refusal falls back and emits diagnostics in Release", () =>
        {
            var trace = new List<string>();
            var handler = new Fixture((_, number) => number == 1 ? new HttpResponseMessage(HttpStatusCode.Forbidden) : Json(Lyrics));
            using (var session = Session(handler, trace.Add)) True(NeteaseApi.RequestLyric(101, session) != null);
            True(trace.Any(line => line.Contains("HTTP 403")));
        });
        Test("Network-down override returns no lyrics without throwing", () =>
        {
            var handler = new Fixture((_, __) => { throw new HttpRequestException("offline"); });
            Equal(null, Lookup(CreatePlugin(handler, (source, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
            Equal(2, handler.Calls.Count);
        });
        Test("Both rejected interfaces return no lyrics through the host callback", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":-460}"));
            Equal(null, Lookup(CreatePlugin(handler), "Singer", "Song", "Album"));
            Equal(6, handler.Calls.Count);
        });
        Test("Library tags missing: callback title and artist are used", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics));
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler), "Singer", "Song", "Album"));
            True(handler.Calls.Count >= 2);
        });
        Test("A host tag-read failure still uses callback metadata", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics));
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler, (_, __) => { throw new InvalidOperationException(); }), "Singer", "Song"));
        });
        Test("Missing title without override skips all network calls", () =>
        {
            var handler = new Fixture((_, __) => Json(Lyrics));
            Equal(null, Lookup(CreatePlugin(handler)));
            Equal(0, handler.Calls.Count);
        });
        Test("Custom10 works without title, artist or duration", () =>
        {
            var handler = new Fixture((_, __) => Json(Lyrics));
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
            Equal(1, handler.Calls.Count);
        });
        Test("A different playing track cannot supply Custom10", () =>
        {
            var handler = new Fixture((_, __) => Json(Lyrics));
            var api = Host();
            var reads = 0;
            api.NowPlaying_GetFileTag = tag => { reads++; return "netease=999"; };
            var plugin = new Plugin(api, () => Session(handler));
            Equal(null, Lookup(plugin));
            Equal(0, handler.Calls.Count);
            Equal(0, reads);
        });
        Test("Bad entries and missing candidate artist/album are handled", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search")
                ? "{\"code\":200,\"result\":{\"songCount\":5,\"songs\":[null,{}, {\"id\":-1,\"name\":\"Song\"},{\"id\":22},{\"id\":101,\"name\":\"Song\"}]}}" : Lyrics));
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler), "Singer", "Song", "Album"));
        });
        Test("Unknown duration does not force the shortest recording", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":200,\"result\":{\"songCount\":2,\"songs\":[{\"id\":1,\"name\":\"Song TV size\",\"duration\":90000},{\"id\":2,\"name\":\"Song\",\"duration\":240000}]}}"));
            using (var session = Session(handler)) Equal(2L, SearchMatch.SearchAndMatch("Song", null, null, null, session));
            Equal(1, handler.Calls.Count); // Identical queries are sent once within a lookup.
        });
        Test("Legacy matching also handles absent artist/album", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics));
            var plugin = CreatePlugin(handler);
            SetConfig(plugin, "{\"UseLegacyMatch\":true}");
            Equal("[00:01.00]Hello", Lookup(plugin, null, "Song"));
        });
        Test("Untimed original is preserved in combined output", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":200,\"lrc\":{\"lyric\":\"Hello\"},\"tlyric\":{\"lyric\":\"Translation\"}}"));
            Equal("Hello", Lookup(CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
        });
        Test("Blank translation is treated as absent", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":200,\"lrc\":{\"lyric\":\"[00:01.00]Hello\"},\"tlyric\":{\"lyric\":\" \"}}"));
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
        });
        Test("Original / translated / combined settings still produce expected output", () =>
        {
            foreach (var format in new[] { 0, 1, 2 })
            {
                var handler = new Fixture((_, __) => Json("{\"code\":200,\"lrc\":{\"lyric\":\"[00:01.00]Hello\"},\"tlyric\":{\"lyric\":\"[00:01.00]Translation\"}}"));
                var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
                SetConfig(plugin, "{\"Format\":" + format + "}");
                Equal(new[] { "[00:01.00]Hello", "[00:01.00]Hello/Translation", "[00:01.00]Translation" }[format], Lookup(plugin));
            }
        });
        Test("Unrelated provider skips network and host access", () =>
        {
            var handler = new Fixture((_, __) => { throw new Exception(); });
            var plugin = CreatePlugin(handler, (_, __) => { throw new Exception(); });
            Equal(null, plugin.RetrieveLyrics("test.flac", null, null, null, true, "Other"));
            Equal(0, handler.Calls.Count);
        });
        Test("Duration accepts fractional seconds and hours", () =>
        {
            Equal((long?)369500, Plugin.ParseDurationString("6:09.500"));
            Equal((long?)3723250, Plugin.ParseDurationString("1:02:03.25"));
            Equal((long?)369500, Plugin.ParseDurationString("369.5"));
        });
        Test("Unknown or corrupt durations are safely ignored", () =>
        {
            foreach (var value in new[] { null, "", " ", "0:00", "-1:10", "abc", "1:60", "1::03", "1:2:3:4", "999999999999999999999999999999", "0:1.0e20" })
                Equal(null, Plugin.ParseDurationString(value));
        });
        Test("Only positive IDs and genuine song routes are accepted", () =>
        {
            foreach (var value in new[] { "netease=101", " NETEASE=101 ", "https://music.163.com/#/song?id=101", "http://music.163.com/song?id=101&foo=bar", "https://music.163.com/m/song?foo=bar&id=101" })
                Equal(101L, Plugin.TryParseNeteaseUrl(value));
            foreach (var value in new[] { null, "", "netease=-1", "netease=0", "netease=999999999999999999999", "https://music.163.com/playlist?id=101", "https://music.163.com/#/artist?id=101", "https://music.163.com/album?id=101", "https://music.163.com.evil.test/song?id=101", "https://evil.test/?music.163.com&song?id=101", "https://music.163.com/song?userid=101", "https://music.163.com/song?id=-101" })
                Equal(0L, Plugin.TryParseNeteaseUrl(value));
        });
        Test("Invalid and null saved configurations open safely", () =>
        {
            foreach (var config in new[] { "null", "{\"Format\":99}", "malformed" })
            {
                var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
                SetConfig(plugin, config);
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    Equal(1, panel.Controls.OfType<ComboBox>().Single().SelectedIndex);
                    plugin.SaveSettings();
                }
                plugin.SaveSettings(); // MusicBee may invoke this after the panel is disposed.
            }
        });
        Test("Fuzzy option is enabled only for legacy matching, initially and after toggling", () =>
        {
            foreach (var legacy in new[] { false, true })
            {
                var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
                SetConfig(plugin, "{\"UseLegacyMatch\":" + (legacy ? "true" : "false") + "}");
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    var checkboxes = panel.Controls.OfType<CheckBox>().ToArray();
                    var mode = checkboxes.Single(box => box.Text.StartsWith("Use legacy"));
                    var fuzzy = checkboxes.Single(box => box.Text.StartsWith("Fuzzy"));
                    Equal(legacy, fuzzy.Enabled);
                    mode.Checked = !legacy;
                    Equal(!legacy, fuzzy.Enabled);
                }
            }
        });
        Test("Saving before opening the settings panel is harmless", () => CreatePlugin(new Fixture((_, __) => Json(Lyrics))).SaveSettings());
        Test("Legacy preference is saved before its old marker is removed", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            var directory = _directories[plugin];
            File.WriteAllText(Path.Combine(directory, "netease_notranslate"), "");
            plugin.MigrateLegacySetting();
            True(!File.Exists(Path.Combine(directory, "netease_notranslate")));
            True(File.ReadAllText(Path.Combine(directory, "netease_config")).Contains("\"Format\":0"));
        });
        Test("A failed migration retains the old preference", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            var directory = _directories[plugin];
            File.WriteAllText(Path.Combine(directory, "netease_notranslate"), "");
            Directory.CreateDirectory(Path.Combine(directory, "netease_config")); // Blocks writing the destination file.
            plugin.MigrateLegacySetting();
            True(File.Exists(Path.Combine(directory, "netease_notranslate")));
        });
        Test("Callback artist and album affect the chosen song", () =>
        {
            var candidates = "{\"code\":200,\"result\":{\"songCount\":2,\"songs\":[{\"id\":1,\"name\":\"Song\",\"artists\":[{\"name\":\"Singer\"}],\"album\":{\"name\":\"Album\"}},{\"id\":2,\"name\":\"Song\",\"artists\":[{\"name\":\"Someone else\"}],\"album\":{\"name\":\"Other\"}}]}}";
            foreach (var artistOnly in new[] { true, false })
            {
                var handler = new Fixture((request, _) =>
                {
                    if (request.RequestUri.AbsolutePath.Contains("search")) return Json(candidates);
                    return Json(request.Method == HttpMethod.Post ? "{\"code\":200}" : Lyrics);
                });
                Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler), artistOnly ? "Singer" : null,
                    "Song", artistOnly ? null : "Album"));
                True(handler.Calls.Last().Contains("id=1&"));
            }
        });
        Test("A timed-out new request can still fall back within the total budget", () =>
        {
            var handler = new Fixture((_, __) => Json(Lyrics)) { StallFirst = true };
            var trace = new List<string>();
            var stopwatch = Stopwatch.StartNew();
            using (var session = new LookupSession(trace.Add, handler, TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(2)))
                True(NeteaseApi.RequestLyric(101, session) != null);
            True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
            Equal(2, handler.Calls.Count);
            True(trace.Any(line => line.Contains("request timed out")));
        });
        Test("The total lookup deadline stops subsequent searches and fallbacks", () =>
        {
            var handler = new Fixture((_, __) => Json(Lyrics)) { StallAll = true };
            var trace = new List<string>();
            var stopwatch = Stopwatch.StartNew();
            using (var session = new LookupSession(trace.Add, handler, TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(250)))
            {
                Equal(0, NeteaseApi.Search("Song", session).Count());
                Equal(0, NeteaseApi.Search("Song Singer", session).Count());
                Equal(null, NeteaseApi.RequestLyric(101, session));
            }
            True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
            Equal(1, handler.Calls.Count);
            True(trace.Any(line => line.Contains("lookup deadline reached")));
        });
        Console.WriteLine("RESULT: " + _passed + " passed, " + _failed + " failed");
        Console.WriteLine("Process architecture: " + (Environment.Is64BitProcess ? "x64" : "x86"));
        return _failed == 0 ? 0 : 1;
    }

    private static LookupSession Session(Fixture handler, Action<string> trace = null) => new LookupSession(trace, handler);
    private static HttpResponseMessage Json(string json) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
    private static Plugin.MusicBeeApiInterface Host() => new Plugin.MusicBeeApiInterface
    {
        Library_GetFileTag = (_, __) => null,
        Library_GetFileProperty = (_, __) => null,
        Setting_GetPersistentStoragePath = () => NewTestDirectory(),
        MB_Trace = _ => { }
    };

    private static Plugin CreatePlugin(Fixture handler, Plugin.Library_GetFileTagDelegate tags = null)
    {
        var api = Host();
        var directory = NewTestDirectory();
        api.Setting_GetPersistentStoragePath = () => directory;
        if (tags != null) api.Library_GetFileTag = tags;
        var plugin = new Plugin(api, () => Session(handler));
        _directories[plugin] = directory;
        return plugin;
    }
    private static readonly Dictionary<Plugin, string> _directories = new Dictionary<Plugin, string>();
    private static string NewTestDirectory()
    {
        var directory = Path.Combine(Environment.CurrentDirectory, "artifacts", "test-data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
    private static void SetConfig(Plugin plugin, string json)
    {
        File.WriteAllText(Path.Combine(_directories[plugin], "netease_config"), json);
        plugin.ReadConfig();
    }
    private static string Lookup(Plugin plugin, string artist = null, string title = null, string album = null) =>
        plugin.RetrieveLyrics("requested.flac", artist, title, album, true, plugin.GetProviders().Single());
    private static void Test(string name, Action action)
    {
        try { action(); _passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { _failed++; Console.WriteLine("FAIL " + name + ": " + ex); }
    }
    private static void True(bool value) { if (!value) throw new Exception("Assertion failed"); }
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual);
    }

    private sealed class Fixture : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _respond;
        internal readonly List<string> Calls = new List<string>();
        internal bool StallFirst, StallAll;
        internal Fixture(Func<HttpRequestMessage, int, HttpResponseMessage> respond) { _respond = respond; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add(request.Method + " " + request.RequestUri);
            if (request.Method == HttpMethod.Post)
            {
                var body = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                True(body.Contains("params=") && body.Contains("encSecKey="));
            }
            if (StallAll || (StallFirst && Calls.Count == 1))
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return _respond(request, Calls.Count);
        }
    }

    private static int LiveSmoke()
    {
        using (var session = new LookupSession(Console.WriteLine))
        {
            var songs = NeteaseApi.Search("周杰伦 晴天", session).ToArray();
            Console.WriteLine("Live search candidates: " + songs.Length);
            var lyrics = NeteaseApi.RequestLyric(186016, session);
            Console.WriteLine("Live fixed-ID lyrics returned: " + !string.IsNullOrWhiteSpace(lyrics?.lrc?.lyric));
            var legacy = session.Request<LyricResult>("lyrics/legacy", HttpMethod.Get,
                "https://music.163.com/api/song/lyric?os=pc&id=186016&lv=-1&kv=-1&tv=-1", null,
                result => result != null && result.code == 200 && !string.IsNullOrWhiteSpace(result.lrc?.lyric));
            Console.WriteLine("Live legacy HTTPS lyrics returned: " + !string.IsNullOrWhiteSpace(legacy?.lrc?.lyric));
            return songs.Length > 0 && !string.IsNullOrWhiteSpace(lyrics?.lrc?.lyric) && legacy != null ? 0 : 1;
        }
    }
}
