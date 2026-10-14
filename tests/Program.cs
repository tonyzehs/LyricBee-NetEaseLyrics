using MusicBeePlugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Reflection;

internal static partial class Program
{
    private const string Lyrics = "{\"code\":200,\"lrc\":{\"lyric\":\"[00:01.00]Hello\"}}";
    private const string Search = "{\"code\":200,\"result\":{\"songCount\":1,\"songs\":[{\"id\":101,\"name\":\"Song\",\"duration\":200000,\"artists\":[{\"name\":\"Singer\"}],\"album\":{\"name\":\"Album\"}}]}}";
    private static int _passed, _failed;

    [STAThread]
    private static int Main(string[] args)
    {
        // These offscreen fixtures pump messages without Application.Run. Keep one
        // UI context alive across forms so asynchronous handlers stay on this STA.
        WindowsFormsSynchronizationContext.AutoInstall = false;
        Control.CheckForIllegalCrossThreadCalls = true;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        var previous = SynchronizationContext.Current;
        using (var context = new WindowsFormsSynchronizationContext())
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try { return Run(args); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
    }

    private static int Run(string[] args)
    {
        if (args.Contains("--lookup-tests"))
        {
            LookupOrderTests(); SavedIdTests(); ShowCreditedTests();
            Console.WriteLine("RESULT: " + _passed + " passed, " + _failed + " failed"); return _failed == 0 ? 0 : 1;
        }
        if (args.Contains("--lookup-replay")) { Console.WriteLine(LookupReplay()); return 0; }
        if (args.Contains("--repair-settings"))
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, File.ReadAllText(args[1]));
            File.Copy(Path.Combine(_directories[plugin], "netease_config"), args[2], true);
            return 0;
        }
        if (args.Contains("--audit-update-tests"))
        {
            AuditUpdateTests(); AuditFollowupTests(); NextAuditTests(); DiagnosticTests(); UpdateTests(); Console.WriteLine("RESULT: " + _passed + " passed, " + _failed + " failed");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Contains("--settings-tests"))
        {
            DisplaySettingsTests(); ContentChoiceTests(); AutomaticLayoutTests();
            Console.WriteLine("RESULT: " + _passed + " passed, " + _failed + " failed");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Contains("--settings-preview")) return SettingsPreview(args.Last());
        if (args.Contains("--layout-tests"))
        {
            AutomaticLayoutTests(); Console.WriteLine("RESULT: " + _passed + " passed, " + _failed + " failed");
            return _failed == 0 ? 0 : 1;
        }
        if (args.Contains("--scaled-settings-preview")) return ScaledSettingsPreview(args.Last());
        if (args.Contains("--live")) return LiveSmoke();
        if (args.Contains("--live-recording")) return LiveRecording();
        if (args.Contains("--live-pronunciation")) return LivePronunciation();
        if (args.Contains("--live-alias")) return LiveArtistAlias();
        if (args.Contains("--cookie-test"))
        {
            CookieTransportTest();
            return _failed == 0 ? 0 : 1;
        }
        if (args.Contains("--audit-test"))
        {
            AuditorTests();
            return _failed == 0 ? 0 : 1;
        }
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
            Equal(3, handler.Calls.Count);
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
        Test("Bad entries and candidates without artist evidence are rejected", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search")
                ? "{\"code\":200,\"result\":{\"songCount\":5,\"songs\":[null,{}, {\"id\":-1,\"name\":\"Song\"},{\"id\":22},{\"id\":101,\"name\":\"Song\"}]}}" : Lyrics));
            Equal(null, Lookup(CreatePlugin(handler), "Singer", "Song", "Album"));
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
        Test("Untimed texts pair in the shared same-line combined output", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":200,\"lrc\":{\"lyric\":\"Hello\"},\"tlyric\":{\"lyric\":\"Translation\"}}"));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
            SetConfig(plugin, "{\"Format\":1}");
            Equal("Hello / Translation", Lookup(plugin));
        });
        Test("Blank translation is treated as absent", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":200,\"lrc\":{\"lyric\":\"[00:01.00]Hello\"},\"tlyric\":{\"lyric\":\" \"}}"));
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
        });
        Test("Legacy translation-only now includes original lyrics while other formats retain their output", () =>
        {
            foreach (var format in new[] { 0, 1, 2 })
            {
                var handler = new Fixture((_, __) => Json("{\"code\":200,\"lrc\":{\"lyric\":\"[00:01.00]Hello\"},\"tlyric\":{\"lyric\":\"[00:01.00]Translation\"}}"));
                var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
                SetConfig(plugin, "{\"Format\":" + format + "}");
                Equal(new[] { "[00:01.00]Hello", "[00:01.000]Hello / Translation", "[00:01.000]Hello\nTranslation" }[format], Lookup(plugin));
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
                    Equal(0, SettingsControls(panel).OfType<ComboBox>().Single(c => c.Name == "OutputLayout").SelectedIndex);
                    plugin.SaveSettings();
                }
                plugin.SaveSettings(); // MusicBee may invoke this after the panel is disposed.
            }
        });
        Test("Single spelling allowance reflects existing settings and remains directly editable", () =>
        {
            foreach (var legacy in new[] { false, true })
            {
                var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
                SetConfig(plugin, "{\"UseLegacyMatch\":" + (legacy ? "true" : "false") + "}");
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    var fuzzy = SettingsControls(panel).OfType<CheckBox>().Single(box => box.Name == "SmallSpellingDifferences");
                    True(fuzzy.Enabled); Equal(!legacy, fuzzy.Checked);
                    fuzzy.Checked = legacy; plugin.SaveSettings();
                }
                plugin.ReadConfig();
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    Equal(legacy, SettingsControls(panel).OfType<CheckBox>().Single(box => box.Name == "SmallSpellingDifferences").Checked);
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
            using (var timeout = new CancellationTokenSource())
            {
            var handler = new Fixture((_, __) => Json(Lyrics)) { BeforeResponse = (_, count, token) =>
                { if (count == 1) { timeout.Cancel(); token.ThrowIfCancellationRequested(); } } };
            var trace = new List<string>();
            using (var session = new LookupSession(trace.Add, handler, requestCancellation: () => timeout.IsCancellationRequested ? CancellationToken.None : timeout.Token))
                True(NeteaseApi.RequestLyric(101, session) != null);
            Equal(2, handler.Calls.Count);
            True(trace.Any(line => line.Contains("request timed out")));
            }
        });
        Test("The total lookup deadline stops subsequent searches and fallbacks", () =>
        {
            using (var deadline = new CancellationTokenSource())
            {
            var handler = new Fixture((_, __) => Json(Lyrics)) { BeforeResponse = (_, __, token) =>
                { deadline.Cancel(); token.ThrowIfCancellationRequested(); } };
            var trace = new List<string>();
            using (var session = new LookupSession(trace.Add, handler, lookupCancellation: deadline.Token))
            {
                Equal(null, session.Request<LyricResult>("deadline fixture", HttpMethod.Get,
                    "https://music.163.com/api/song/lyric?id=101", null, r => r != null));
                Equal(0, NeteaseApi.Search("Song", session).Count());
                Equal(0, NeteaseApi.Search("Song Singer", session).Count());
                Equal(null, NeteaseApi.RequestLyric(101, session));
            }
            Equal(1, handler.Calls.Count);
            True(trace.Any(line => line.Contains("lookup deadline reached")));
            }
        });
        CookieTransportTest();
        AuditorTests();
        TwoLineTests();
        MatchingTests();
        ArtistAliasTests();
        AuditUpdateTests();
        AuditFollowupTests();
        NextAuditTests();
        DiagnosticTests();
        UpdateTests();
        AutomaticLayoutTests();
        Console.WriteLine("RESULT: " + _passed + " passed, " + _failed + " failed");
        Console.WriteLine("Process architecture: " + (Environment.Is64BitProcess ? "x64" : "x86"));
        return _failed == 0 ? 0 : 1;
    }

    private static LookupSession Session(Fixture handler, Action<string> trace = null) => new LookupSession(trace, handler);

    private static void AuditorTests()
    {
        Test("Redirects cannot silently choose an unencrypted request outside the fallback policy", () =>
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = Task.Run(async () =>
            {
                var accept = listener.AcceptTcpClientAsync();
                if (await Task.WhenAny(accept, Task.Delay(5000)) != accept)
                    throw new TimeoutException("Local redirect fixture received no request");
                using (var client = await accept)
                {
                    client.ReceiveTimeout = 5000;
                    using (var stream = client.GetStream())
                    using (var reader = new StreamReader(stream, System.Text.Encoding.ASCII, false, 1024, true))
                    {
                        while (!string.IsNullOrEmpty(reader.ReadLine())) { }
                        var response = System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: " +
                            url + "/plain\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(response, 0, response.Length);
                    }
                }
                listener.Stop();
            });
            var trace = new List<string>();
            var completed = false;
            try
            {
                using (var session = new LookupSession(trace.Add))
                    Equal(null, session.Request<ApiResponse>("redirect fixture", HttpMethod.Get, url, null, r => r != null));
                True(trace.Any(t => t.Contains("HTTP 302")));
                completed = true;
            }
            finally
            {
                listener.Stop();
                try { server.GetAwaiter().GetResult(); }
                catch when (!completed) { }
            }
        });
        Test("A valid secure response never triggers HTTP fallback", () =>
        {
            var handler = new Fixture((request, _) =>
            {
                True(request.RequestUri.Scheme == "https");
                return Json(request.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics);
            });
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler), "Singer", "Song", "Album"));
            Equal(2, handler.Calls.Count);
        });
        Test("A phone share URL retrieves lyrics without requiring title tags", () =>
        {
            var handler = new Fixture((_, __) => Json(Lyrics));
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler,
                (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "https://y.music.163.com/m/song?id=101&uct2=example" : null)));
            Equal(1, handler.Calls.Count);
        });
        Test("Blocked HTTPS falls back to HTTP for both search and lyrics", () =>
        {
            var handler = new Fixture((request, _) =>
            {
                if (request.RequestUri.Scheme == "https") throw new HttpRequestException("TLS blocked");
                return Json(request.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics);
            });
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler), "Singer", "Song", "Album"));
            Equal(4, handler.Calls.Count);
            True(handler.Calls[0].StartsWith("POST https://") && handler.Calls[1].StartsWith("GET https://"));
            True(handler.Calls[2].StartsWith("GET http://") && handler.Calls[3].StartsWith("GET http://"));
        });
        Test("Silently stalled HTTPS leaves time for HTTP search and lyric recovery", () =>
        {
            using (var timeout = new CancellationTokenSource())
            using (var legacyTimeout = new CancellationTokenSource())
            {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics))
                { BeforeResponse = (request, _, token) => { if (request.RequestUri.Scheme == "https")
                    { (request.Method == HttpMethod.Post ? timeout : legacyTimeout).Cancel(); token.ThrowIfCancellationRequested(); } } };
            using (var session = new LookupSession(null, handler, requestCancellation: () => handler.Calls.Count == 0 ? timeout.Token :
                handler.Calls.Count == 1 ? legacyTimeout.Token : CancellationToken.None))
            {
                Equal(101L, SearchMatch.SearchAndMatch("Song", "Singer", "Album", null, session));
                Equal("[00:01.00]Hello", NeteaseApi.RequestLyric(101, session)?.lrc?.lyric);
            }
            Equal(1, handler.Calls.Count(c => c.StartsWith("POST ")));
            Equal(2, handler.Calls.Count(c => c.StartsWith("GET http://")));
            }
        });
        Test("Turning off HTTP fallback prevents unencrypted search and lyric requests", () =>
        {
            foreach (var useOverride in new[] { false, true })
            {
                var handler = new Fixture((request, _) =>
                {
                    if (request.RequestUri.Scheme == "https") throw new HttpRequestException("TLS blocked");
                    throw new Exception("HTTP must not be called");
                });
                var plugin = CreatePlugin(handler, (_, tag) => useOverride && tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
                SetConfig(plugin, "{\"AllowInsecureHttp\":false}");
                Equal(null, Lookup(plugin, "Singer", "Song", "Album"));
                Equal(2, handler.Calls.Count);
                True(handler.Calls.All(c => c.Contains("https://")));
            }
        });
        Test("The HTTP setting defaults on for existing configurations and saves the user's opt-out", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, "{\"Format\":0}");
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                var option = SettingsControls(panel).OfType<CheckBox>().Single(c => c.Text.StartsWith("Allow HTTP"));
                True(option.Checked);
                option.Checked = false;
                plugin.SaveSettings();
            }
            plugin.ReadConfig();
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                True(!SettingsControls(panel).OfType<CheckBox>().Single(c => c.Text.StartsWith("Allow HTTP")).Checked);
            }
        });
        Test("The search deadline preserves part of the lookup for available lyrics", () =>
        {
            using (var deadline = new CancellationTokenSource())
            {
            var handler = new Fixture((_, __) => Json(Lyrics)) { BeforeResponse = (request, _, token) =>
                { if (request.RequestUri.AbsolutePath.Contains("search")) { deadline.Cancel(); token.ThrowIfCancellationRequested(); } } };
            using (var session = new LookupSession(null, handler, searchCancellation: deadline.Token))
            {
                Equal(0, NeteaseApi.Search("Song", session).Count());
                Equal(0, NeteaseApi.Search("Another query", session).Count());
                True(NeteaseApi.RequestLyric(101, session) != null);
            }
            }
        });
        Test("Application version cookie is sent only to legacy interfaces and no login cookies are sent", () =>
        {
            var handler = new Fixture((request, _) =>
            {
                if (request.Method == HttpMethod.Post)
                {
                    True(!request.Headers.Contains("Cookie"));
                    return Json("{\"code\":200}");
                }
                Equal("appver=1.5.0.75771;", string.Join("", request.Headers.GetValues("Cookie")));
                return Json(Lyrics);
            });
            using (var session = Session(handler)) True(NeteaseApi.RequestLyric(101, session) != null);
        });
        Test("Phone share song links are accepted without accepting arbitrary subdomains", () =>
        {
            Equal(400875204L, Plugin.TryParseNeteaseUrl("https://y.music.163.com/m/song?id=400875204&uct2=example"));
            Equal(0L, Plugin.TryParseNeteaseUrl("https://y.music.163.com.evil.test/m/song?id=400875204"));
            Equal(0L, Plugin.TryParseNeteaseUrl("https://y.music.163.com/m/playlist?id=400875204"));
        });
        Test("A timed-out new search cannot starve the working legacy lyric download", () =>
        {
            using (var timeout = new CancellationTokenSource())
            {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics))
                { BeforeResponse = (request, _, token) => { if (request.Method == HttpMethod.Post)
                    { timeout.Cancel(); token.ThrowIfCancellationRequested(); } } };
            using (var session = new LookupSession(null, handler, requestCancellation: () => timeout.IsCancellationRequested ? CancellationToken.None : timeout.Token))
            {
                Equal(101L, SearchMatch.SearchAndMatch("Song", "Singer", "Different album", null, session));
                Equal("[00:01.00]Hello", NeteaseApi.RequestLyric(101, session)?.lrc?.lyric);
            }
            Equal(1, handler.Calls.Count(c => c.StartsWith("POST ")));
            True(handler.Calls.Last().Contains("id=101&"));
            }
        });
        Test("Instrumental placeholders return no lyrics and do not request fallback lyrics", () =>
        {
            foreach (var text in new[] { "纯音乐，请欣赏", "[00:00.000]纯音乐，请欣赏\n[00:01.000]", "[ti:Instrumental]\n[00:00.00]純音樂，請欣賞" })
            {
                var handler = new Fixture((_, __) => Json(JsonConvert.SerializeObject(new { code = 200, lrc = new { lyric = text } })));
                Equal(null, Lookup(CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
                Equal(1, handler.Calls.Count);
            }
        });
        Test("Real lyrics containing an instrumental phrase remain intact", () =>
        {
            var text = "[00:01.00]纯音乐，请欣赏\n[00:02.00]Actual singing";
            var handler = new Fixture((_, __) => Json(JsonConvert.SerializeObject(new { code = 200, lrc = new { lyric = text } })));
            Equal(text, Lookup(CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
        });
        Test("An explicit instrumental flag suppresses even a nonempty placeholder body", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":200,\"nolyric\":true,\"lrc\":{\"lyric\":\"instrumental\"}}"));
            Equal(null, Lookup(CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
            Equal(1, handler.Calls.Count);
        });
    }

    private static void CookieTransportTest()
    {
        Test("Server cookies cannot change later anonymous searches in the same lookup", () =>
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/search";
            var server = Task.Run(async () =>
            {
                for (var index = 0; index < 2; index++)
                {
                    var accept = listener.AcceptTcpClientAsync();
                    if (await Task.WhenAny(accept, Task.Delay(5000)) != accept)
                        throw new TimeoutException("Local fixture did not receive a request");
                    using (var client = await accept)
                    {
                        client.ReceiveTimeout = 5000;
                        using (var stream = client.GetStream())
                        using (var reader = new StreamReader(stream, System.Text.Encoding.ASCII, false, 1024, true))
                        {
                            var headers = new List<string>();
                            string line;
                            while (!string.IsNullOrEmpty(line = reader.ReadLine())) headers.Add(line);
                            var poisoned = headers.Any(h => h.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase)
                                && h.Contains("search-state=poison"));
                            var body = System.Text.Encoding.UTF8.GetBytes(poisoned
                                ? SearchJson(new[] { Song(999, "Unrelated song", "Other artist", "Other album", 200000) })
                                : Search);
                            var response = System.Text.Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n" +
                                "Set-Cookie: search-state=poison; Path=/\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                            await stream.WriteAsync(response, 0, response.Length);
                            await stream.WriteAsync(body, 0, body.Length);
                        }
                    }
                }
            });
            var completed = false;
            try
            {
                using (var session = new LookupSession(Console.WriteLine))
                {
                    for (var index = 0; index < 2; index++)
                    {
                        var result = session.Request<SearchResult>("cookie fixture", HttpMethod.Get, url, null,
                            r => r != null && r.code == 200 && r.result?.songs != null);
                        Equal(101L, result?.result?.songs.Single().id ?? 0);
                    }
                }
                completed = true;
            }
            finally
            {
                listener.Stop();
                try { server.GetAwaiter().GetResult(); }
                catch when (!completed) { /* Preserve the request/assertion failure during fixture shutdown. */ }
            }
        });
    }

    private static void TwoLineTests()
    {
        FormattingCompatibilityTests();
        PronunciationTests();
        DisplaySettingsTests();
        Test("Default host output uses two lines with one timestamp and no added slash", () =>
        {
            var handler = new Fixture((_, __) => Json(JsonConvert.SerializeObject(new
            {
                code = 200, lrc = new { lyric = "[00:01.00]日本語\n[00:03.00]次の行" },
                tlyric = new { lyric = "[00:01.000]中文\n[00:03.00]下一行" }
            })));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
            Equal("[00:01.000]日本語\n中文\n[00:03.000]次の行\n下一行", Lookup(plugin));
        });
        Test("Missing format uses the two-line default while explicit saved choices are retained", () =>
        {
            var scenarios = new Dictionary<string, int>
            {
                ["{}"] = 0, ["{\"AllowInsecureHttp\":false}"] = 0,
                ["{\"Format\":0}"] = 0, ["{\"Format\":1}"] = 1, ["{\"Format\":2}"] = 0
            };
            foreach (var scenario in scenarios)
            {
                var handler = new Fixture((_, __) => Json("{\"code\":200,\"lrc\":{\"lyric\":\"Hello\"},\"tlyric\":{\"lyric\":\"Translation\"}}"));
                var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
                SetConfig(plugin, scenario.Key);
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    Equal(scenario.Value, SettingsControls(panel).OfType<ComboBox>().Single(c => c.Name == "OutputLayout").SelectedIndex);
                }
                Equal(scenario.Key == "{\"Format\":0}" ? "Hello" : "Hello / Translation", Lookup(plugin));
            }
        });
        Test("Two-line format persists across closing and reopening the settings", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, "{\"Format\":3}");
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                Equal(0, SettingsControls(panel).OfType<ComboBox>().Single(c => c.Name == "OutputLayout").SelectedIndex);
                plugin.SaveSettings();
            }
            var saved = JsonConvert.DeserializeObject<NeteaseConfig>(File.ReadAllText(Path.Combine(_directories[plugin], "netease_config")));
            Equal(NeteaseConfig.OutputFormat.BothTwoLines, saved.Format);
            plugin.ReadConfig();
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                Equal(0, SettingsControls(panel).OfType<ComboBox>().Single(c => c.Name == "OutputLayout").SelectedIndex);
            }
        });
        Test("Two-line mode leaves blank or missing translations alone", () =>
            Equal("[00:01.000]Original\n[00:02.000]Second\n[00:03.000]",
                LyricProcessor.InjectTranslationTwoLines("[00:01.00]Original\n[00:02.00]Second\n[00:03.00]",
                    "[00:01.00] \n[00:03.00]Rest translation")));
        Test("Two-line mode normalizes equivalent timestamp spellings and sorts numerically", () =>
            Equal("[02:00.000]First\nOne\n[10:00.100]Later\nTwo",
                LyricProcessor.InjectTranslationTwoLines("[10:00.1]Later\n[2:00]First",
                    "[02:00.000]One\n[10:00:10]Two")));
        Test("Two-line mode preserves original metadata and aligns differing offsets", () =>
            Equal("[ti:Title]\n[offset: +500]\n[00:01.000]Original\nTranslation",
                LyricProcessor.InjectTranslationTwoLines("[ti:Title]\n[offset: +500]\n[00:01]Original",
                    "[offset:-500]\n[00:00.00]Translation")));
        Test("Raw alignment recovers translation while preserving the original playback offset", () =>
            Equal("[offset:1000]\n[00:02.000]Original\nUnaligned",
                LyricProcessor.InjectTranslationTwoLines("[offset:1000]\n[00:02]Original", "[offset:0]\n[00:02]Unaligned")));
        Test("Multiple timestamps can attach different translations to repeated originals", () =>
            Equal("[00:01.000]Repeated\nOne\n[00:02.000]Repeated\nTwo",
                LyricProcessor.InjectTranslationTwoLines("[00:01][00:02.00]Repeated", "[00:01]One\n[00:02]Two")));
        Test("Simultaneous lines form one cue and retain all distinct text", () =>
            Equal("[00:01.000]One\nTwo\nFirst\nSecond",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\n[00:01.00]Two",
                    "[00:01]First\n[00:01]Second\n[00:01.000]First")));
        Test("Existing continuation lines remain inside their original cue", () =>
            Equal("[00:01.000]One\nContinuation\nTranslation\nTranslation continuation",
                LyricProcessor.InjectTranslationTwoLines("[00:01]One\nContinuation", "[00:01]Translation\nTranslation continuation")));
        Test("Literal slash characters in lyrics are retained", () =>
            Equal("[00:01.000]AC/DC / Original\nTranslation / Text",
                LyricProcessor.InjectTranslationTwoLines("[00:01]AC/DC / Original", "[00:01]Translation / Text")));
        Test("Untimed unequal line counts preserve verse line breaks", () =>
            Equal("One\nTwo\n\nTranslated paragraph",
                LyricProcessor.InjectTranslationTwoLines("One\nTwo", "Translated paragraph")));
        Test("Untimed original does not inherit translation timing", () =>
            Equal("Original / Translation",
                LyricProcessor.InjectTranslationTwoLines("Original", "[00:01]Translation")));
        Test("Timed original with untimed translation is retained without invented alignment", () =>
            Equal("[00:01]Original", LyricProcessor.InjectTranslationTwoLines("[00:01]Original", "Unaligned translation")));
        Test("Newline conventions and UTF-8 BOM do not obstruct two-line alignment", () =>
            Equal("[00:01.000]Original\nTranslation\n[00:02.000]Second\nNext",
                LyricProcessor.InjectTranslationTwoLines("\uFEFF[00:01]Original\r\n[00:02]Second\r\n",
                    "\uFEFF[00:01]Translation\r[00:02]Next\r")));
        Test("Two-line absent translation returns the unmodified original", () =>
        {
            Equal("[00:01]Original", LyricProcessor.InjectTranslationTwoLines("[00:01]Original", null));
            Equal("[00:01]Original", LyricProcessor.InjectTranslationTwoLines("[00:01]Original", " \n"));
        });
        Test("Untimed line pairs use a spaced slash and retain original verse breaks", () =>
            Equal("One / First\nTwo / Second\n\nThree / Third",
                LyricProcessor.InjectTranslationTwoLines("One\nTwo\n\nThree", "First\nSecond\nThird")));
        Test("Untimed verse pairs retain both texts when translation splits a line", () =>
            Equal("One\nTwo\n\nFirst\n\nThree\n\nSecond\nThird\nFourth",
                LyricProcessor.InjectTranslationTwoLines("One\nTwo\n\nThree", "First\n\nSecond\nThird\nFourth")));
        Test("Untimed unpairable structures preserve the original instead of guessing", () =>
            Equal("One\n\nTwo", LyricProcessor.InjectTranslationTwoLines("One\n\nTwo", "First\nSecond\nThird")));
        Test("Untimed metadata is retained but never treated as a lyric pair", () =>
            Equal("[ti:Original title]\nAC/DC / 中文 / 译文",
                LyricProcessor.InjectTranslationTwoLines("\uFEFF[ti:Original title]\r\nAC/DC / 中文",
                    "[ti:Translated title]\r\n译文")));
        Test("Untimed blank or metadata-only translations leave originals unchanged", () =>
        {
            Equal("Original", LyricProcessor.InjectTranslationTwoLines("Original", "[ti:Translation]\n\n"));
            Equal("Original", LyricProcessor.InjectTranslationTwoLines("Original", "[00:01]\n"));
        });
    }

    private static void MatchingTests()
    {
        MatchingAuditTests(); LookupOrderTests(); SavedIdTests(); ShowCreditedTests();
        SoundtrackTests();
        LocalizedSoundtrackTests();
        Test("Captured By Your Side search results are rejected rather than choosing another song", () =>
        {
            var fixture = ReadRecordingFixture();
            Equal(0L, Select(fixture.observed, fixture.title, fixture.artist, fixture.album, fixture.duration_ms));
        });
        Test("The supplied NetEase recording matches singer-plus-composer tags and a Japanese album name", () =>
        {
            var fixture = ReadRecordingFixture();
            Equal(400875204L, Select(fixture.observed.Concat(new[] { fixture.correct }), fixture.title,
                fixture.artist, fixture.album, fixture.duration_ms));
        });
        Test("Captured irrelevant responses do not trigger a lyric download through MusicBee", () =>
        {
            var fixture = ReadRecordingFixture();
            var json = SearchJson(fixture.observed);
            var handler = new Fixture((_, __) => Json(json));
            Equal(null, Lookup(CreatePlugin(handler), fixture.artist, fixture.title, fixture.album));
            True(handler.Calls.All(c => c.Contains("/search/")));
        });
        Test("Legacy search recovers a valid song when code=200 new results are irrelevant", () =>
        {
            var fixture = ReadRecordingFixture();
            var handler = new Fixture((request, _) =>
            {
                if (!request.RequestUri.AbsolutePath.Contains("search")) return Json(Lyrics);
                return Json(SearchJson(request.Method == HttpMethod.Get ? new[] { fixture.correct } : fixture.observed));
            });
            using (var session = Session(handler))
                Equal(400875204L, SearchMatch.SearchAndMatch(fixture.title, fixture.artist, fixture.album, fixture.duration_ms, session));
            True(handler.Calls.Any(c => c.StartsWith("GET ") && Uri.UnescapeDataString(c).Contains("By Your Side 澤田かおり")));
        });
        Test("Legacy fuzzy mode rejects the captured unrelated songs", () =>
        {
            var fixture = ReadRecordingFixture();
            var handler = new Fixture((_, __) => Json(SearchJson(fixture.observed)));
            using (var session = Session(handler))
                Equal(0L, SearchMatchLegacy.QueryWithFeatRemoved(fixture.title, fixture.artist, true, session));
        });
        Test("An exact title with the wrong artist is rejected even when album and duration agree", () =>
            Equal(0L, Select(new[] { Song(1, "Song", "Other singer", "Album", 200000) })));
        Test("An unrelated title with the right artist, album and duration is rejected", () =>
            Equal(0L, Select(new[] { Song(1, "Different song", "Singer", "Album", 200000) })));
        Test("Matching the first title word does not qualify a song", () =>
            Equal(0L, Select(new[] { Song(1, "By Your Heart", "Singer", "Album", 200000) }, "By Your Side")));
        Test("An exact title five seconds off beats a wrong title of identical duration", () =>
            Equal(1L, Select(new[] { Song(1, "Song", "Singer", "Album", 205000), Song(2, "Wrong", "Singer", "Album", 200000) })));
        Test("Artist evidence outweighs a close duration for the wrong artist", () =>
            Equal(1L, Select(new[] { Song(1, "Song", "Singer", "Album", 205000), Song(2, "Song", "Other", "Album", 200000) })));
        Test("A secondary composer credit cannot identify another recording by itself", () =>
            Equal(0L, Select(new[] { Song(1, "Song", "Composer", "Other album", 200000) }, artist: "Singer/Composer")));
        Test("A similar album cannot corroborate a secondary composer without the primary singer", () =>
            Equal(0L, Select(new[] { Song(1, "Song", "Composer", "Soundtrack 03", 200000) },
                artist: "Singer/Composer", album: "Soundtrack 02")));
        Test("Reordered service credits need corroborating album and duration", () =>
            Equal(1L, Select(new[] { Song(1, "Song", "Singer", "Album", 200000) }, artist: "Composer/Singer")));
        Test("An exact album resolves recordings with the same title and artist", () =>
            Equal(1L, Select(new[] { Song(1, "Song", "Singer", "Album", 200000), Song(2, "Song", "Singer", "Completely different", 200000) })));
        Test("An exact album also distinguishes a similarly named deluxe album", () =>
            Equal(1L, Select(new[] { Song(1, "Song", "Singer", "Album", 200000), Song(2, "Song", "Singer", "Album Deluxe", 200000) })));
        Test("A translated album does not exclude a matching title, artist and duration", () =>
            Equal(1L, Select(new[] { Song(1, "Song", "Singer", "English soundtrack name", 200000) }, album: "日本語のサウンドトラック")));
        Test("Ambiguous recordings are rejected regardless of ID and response order", () =>
        {
            var songs = new[] { Song(1, "Song", "Singer", "One", 200000), Song(999, "Song", "Singer", "Two", 205000) };
            Equal(0L, Select(songs, album: null));
            Equal(0L, Select(songs.Reverse(), album: null));
        });
        Test("Duplicate appearances of the same ID do not create ambiguity", () =>
        {
            var song = Song(1, "Song", "Singer", "Album", 200000);
            Equal(1L, Select(new[] { song, song }));
        });
        Test("Confirmed duration beats an otherwise identical candidate of unknown duration", () =>
            Equal(1L, Select(new[] { Song(1, "Song", "Singer", "Album", 200000), Song(2, "Song", "Singer", "Album", 0) })));
        Test("A very different recording length is rejected", () =>
            Equal(0L, Select(new[] { Song(1, "Song", "Singer", "Album", 90000) })));
        Test("An unknown local duration does not resolve ambiguous recordings by shortest length", () =>
            Equal(0L, Select(new[] { Song(1, "Song", "Singer", "Album", 90000), Song(2, "Song", "Singer", "Album", 200000) }, duration: null)));
        Test("Full-width punctuation, spacing and title case normalize consistently", () =>
            Equal(1L, Select(new[] { Song(1, "ＢＹ　ＹＯＵＲ　ＳＩＤＥ！", "Singer", "Album", 200000) }, "By Your Side")));
        Test("A small typo on a long title can match, but cannot bypass the artist check", () =>
        {
            Equal(1L, Select(new[] { Song(1, "By Your Sile", "Singer", "Album", 200000) }, "By Your Side"));
            Equal(0L, Select(new[] { Song(1, "By Your Sile", "Other", "Album", 200000) }, "By Your Side"));
        });
        Test("Legacy exact mode refuses a spelling difference while fuzzy mode accepts it safely", () =>
        {
            foreach (var fuzzy in new[] { false, true })
            {
                var handler = new Fixture((_, __) => Json(SearchJson(new[] { Song(1, "Beautiful Songs", "Singer", "Album", 200000) })));
                using (var session = Session(handler))
                    Equal(fuzzy ? 1L : 0L, SearchMatchLegacy.QueryWithFeatRemoved("Beautiful Song", "Singer", fuzzy, session));
            }
        });
        Test("Live, remix and TV-size recordings do not silently replace the requested edition", () =>
        {
            foreach (var suffix in new[] { " (Live)", " (Remix)", " (TV)", " (TV size)", " - Acoustic" })
                Equal(0L, Select(new[] { Song(1, "A Very Long Meaningful Song Title" + suffix, "Singer", "Album", 200000) }, "A Very Long Meaningful Song Title"));
        });
        Test("A matching live edition remains eligible", () =>
            Equal(1L, Select(new[] { Song(1, "Song (Live)", "Singer", "Album", 200000) }, "Song (Live)")));
        Test("Feather and feature are not interpreted as featured artist credits", () =>
        {
            foreach (var title in new[] { "Birds of a Feather", "A feature film", "Feather" })
            {
                var split = SearchMatch.SplitTitleArtist(title, "Singer");
                Equal(title, split.Item1);
                Equal("Singer", string.Join(";", split.Item2));
            }
        });
        Test("Featuring credits are extracted as complete names", () =>
        {
            foreach (var credit in new[] { " feat. Guest", " Featuring Guest", " (ft. Guest)" })
            {
                var split = SearchMatch.SplitTitleArtist("Song" + credit, "Singer");
                Equal("Song", split.Item1);
                Equal("Singer;Guest", string.Join(";", split.Item2));
            }
        });
        Test("Real artist names retain slashes, ampersands, commas and middle dots", () =>
        {
            foreach (var name in new[] { "AC/DC", "ac/dc", "Simon & Garfunkel", "Tyler, The Creator", "迈克尔·杰克逊" })
            {
                Equal(name, SearchMatch.SplitTitleArtist("Song", name).Item2.Single());
                Equal(1L, Select(new[] { Song(1, "Song", name, "Album", 200000) }, artist: name));
            }
        });
        Test("Partial names created from punctuation do not match solo artists", () =>
        {
            foreach (var pair in new[] { new[] { "AC/DC", "AC" }, new[] { "Simon & Garfunkel", "Simon" }, new[] { "Tyler, The Creator", "Tyler" }, new[] { "迈克尔·杰克逊", "迈克尔" } })
                Equal(0L, Select(new[] { Song(1, "Song", pair[1], "Album", 200000) }, artist: pair[0]));
        });
        Test("Japanese compatibility characters and explicit artist separators normalize safely", () =>
        {
            Equal("澤田かおり;服部隆之", string.Join(";", SearchMatch.SplitTitleArtist("Song", "澤田かおり/服部隆之").Item2));
            Equal("澤田かおり;服部隆之", string.Join(";", SearchMatch.SplitTitleArtist("Song", "澤田かおり、服部隆之").Item2));
            Equal(1L, Select(new[] { Song(1, "Song", "服部隆之", "Album", 200000) }, artist: "服部隆之"));
        });
        Test("An unsplit collaboration credit can match the service's separate artist fields", () =>
        {
            var song = Song(1, "Song", "Jonas Blue", "Album", 200000);
            song.artists = new[] { new SearchResultArtist { name = "Jonas Blue" }, new SearchResultArtist { name = "RAYE" } };
            Equal(1L, Select(new[] { song }, artist: "Jonas Blue & RAYE"));
        });
        Test("When artist is absent, a contradictory supplied album is rejected", () =>
            Equal(0L, Select(new[] { Song(1, "Song", "Singer", "Other album", 200000) }, artist: null)));
        Test("Ambiguous search results do not trigger a lyric download", () =>
        {
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { Song(1, "Song", "Singer", "One", 200000), Song(2, "Song", "Singer", "Two", 205000) })));
            Equal(null, Lookup(CreatePlugin(handler), "Singer", "Song"));
            True(handler.Calls.All(c => c.Contains("/search/")));
        });
    }

    private static SearchResultSong Song(long id, string title, string artist, string album, long duration) =>
        new SearchResultSong { id = id, name = title, duration = duration,
            artists = new[] { new SearchResultArtist { name = artist } }, album = new SearchResultAlbum { name = album } };
    private static long Select(IEnumerable<SearchResultSong> songs, string title = "Song", string artist = "Singer", string album = "Album", long? duration = 200000) =>
        SearchMatch.SelectCandidate(songs, title, artist, album, duration);
    private static string SearchJson(IEnumerable<SearchResultSong> songs)
    {
        var values = songs.ToArray();
        return JsonConvert.SerializeObject(new { code = 200, result = new { songCount = values.Length, songs = values } });
    }
    private static RecordingFixture ReadRecordingFixture()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NeteaseLyricsTest.Fixtures.ByYourSide.json"))
        using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
            return JsonConvert.DeserializeObject<RecordingFixture>(reader.ReadToEnd());
    }
    private sealed class RecordingFixture
    {
        public string title { get; set; }
        public string artist { get; set; }
        public string album { get; set; }
        public long duration_ms { get; set; }
        public SearchResultSong correct { get; set; }
        public SearchResultSong[] observed { get; set; }
    }
    private static HttpResponseMessage Json(string json) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
    private static Plugin.MusicBeeApiInterface Host() => new Plugin.MusicBeeApiInterface
    {
        Library_GetFileTag = (_, __) => null,
        Library_GetFileProperty = (_, __) => null,
        Setting_GetPersistentStoragePath = () => NewTestDirectory(),
        MB_Trace = _ => { }
    };

    private static Plugin CreatePlugin(Fixture handler, Plugin.Library_GetFileTagDelegate tags = null, string duration = null)
    {
        var api = Host();
        var directory = NewTestDirectory();
        api.Setting_GetPersistentStoragePath = () => directory;
        if (tags != null) api.Library_GetFileTag = tags;
        if (duration != null) api.Library_GetFileProperty = (_, __) => duration;
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
        internal bool StallNew, StallHttps, StallLanguage;
        internal Action<HttpRequestMessage, int, CancellationToken> BeforeResponse;
        internal Fixture(Func<HttpRequestMessage, int, HttpResponseMessage> respond) { _respond = respond; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add(request.Method + " " + request.RequestUri);
            if (request.Method == HttpMethod.Post)
            {
                var body = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                True(body.Contains("params=") && body.Contains("encSecKey="));
            }
            BeforeResponse?.Invoke(request, Calls.Count, cancellationToken);
            if ((StallNew && request.Method == HttpMethod.Post) ||
                (StallHttps && request.RequestUri.Scheme == "https") ||
                (StallLanguage && request.RequestUri.AbsolutePath.Contains("/about/")))
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

    private static int LiveRecording()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var fixture = ReadRecordingFixture();
        string expected;
        using (var session = new LookupSession(Console.WriteLine))
            expected = NeteaseApi.RequestLyric(fixture.correct.id, session)?.lrc?.lyric;
        if (string.IsNullOrWhiteSpace(expected))
        {
            Console.WriteLine("Cannot verify: reference lyrics unavailable");
            return 1;
        }
        using (var session = new LookupSession(Console.WriteLine))
        {
            // Reproduce the earlier investigative probe's cookie-contaminated request sequence.
            Console.WriteLine("Preliminary broad title search count: " + NeteaseApi.Search(fixture.title, session).Count());
            var selected = SearchMatch.SearchAndMatch(fixture.title, fixture.artist, fixture.album, fixture.duration_ms, session);
            Console.WriteLine("Automatic song ID after preliminary search: " + selected);
            if (selected != fixture.correct.id) return 1;
            var original = NeteaseApi.RequestLyric(selected, session)?.lrc?.lyric;
            Console.WriteLine("Automatic lyrics equal reference recording: " + (original == expected));
            if (original != expected) return 1;
        }
        foreach (var legacy in new[] { false, true })
        {
            var api = Host();
            var directory = NewTestDirectory();
            api.Setting_GetPersistentStoragePath = () => directory;
            api.Library_GetFileTag = (_, tag) => tag == Plugin.MetaDataType.TrackTitle ? fixture.title
                : tag == Plugin.MetaDataType.Artist ? fixture.artist
                : tag == Plugin.MetaDataType.Album ? fixture.album : null;
            api.Library_GetFileProperty = (_, property) => property == Plugin.FilePropertyType.Duration ? "4:46.300" : null;
            var plugin = new Plugin(api, () => new LookupSession(Console.WriteLine));
            _directories[plugin] = directory;
            SetConfig(plugin, JsonConvert.SerializeObject(new NeteaseConfig
                { UseLegacyMatch = legacy, Fuzzy = true, Format = NeteaseConfig.OutputFormat.Original }));
            var original = Lookup(plugin);
            Console.WriteLine("MusicBee callback mode=" + (legacy ? "legacy" : "default") +
                ", Custom10 absent, lyrics equal correct recording: " + (original == expected));
            if (original != expected) return 1;
        }
        Console.WriteLine("LIVE RECORDING: passed; no downloaded lyrics saved");
        return 0;
    }
}
