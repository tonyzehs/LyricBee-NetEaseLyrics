using MusicBeePlugin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;

internal static partial class Program
{
    private static JObject AliasRecording()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NeteaseLyricsTest.Fixtures.KienRomance.json"))
        using (var reader = new StreamReader(stream)) return JObject.Parse(reader.ReadToEnd());
    }
    private static bool ArtistCall(string call) => call.Contains("/api/artist/");
    private static string AliasDetails(long id = 81, string name = "Localized", params string[] aliases) =>
        JsonConvert.SerializeObject(new { code = 200, artist = new { id, name, alias = aliases } });
    private static SearchResultSong AliasSong(long songId = 1, long artistId = 81, string artist = "Localized")
    {
        var song = Song(songId, "Song", artist, "Album", 200000); song.artists[0].id = artistId; return song;
    }
    private static Fixture AliasFixture(SearchResultSong[] songs, string details = null) =>
        new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("/api/artist/") ?
            details ?? AliasDetails(81, "Localized", "Singer") : r.RequestUri.AbsolutePath.Contains("lyric") ? Lyrics : SearchJson(songs)));
    private static long[] AliasLookup(Fixture handler, string title = "Song", string artist = "Singer", string album = "Album", long? duration = 200000,
        ArtistAliasCache cache = null)
    {
        using (var session = Session(handler))
        {
            if (cache != null) session.AliasCache = cache;
            return SearchMatch.SearchCandidates(title, artist, album, duration, session);
        }
    }
    private static void ArtistAliasTests()
    {
        Test("Recorded Kien Romance fails without aliases and succeeds using verified artist metadata", () =>
        {
            var f = AliasRecording();
            var songs = f["search"]["result"]["songs"].ToObject<SearchResultSong[]>();
            Equal(0L, SearchMatch.SelectCandidate(songs, (string)f["title"], (string)f["artist"], (string)f["album"], (long)f["duration_ms"]));
            var handler = new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("/api/artist/") ? f["details"].ToString() : f["search"].ToString()));
            var ids = AliasLookup(handler, (string)f["title"], (string)f["artist"], (string)f["album"], (long)f["duration_ms"]);
            Equal((long)f["correct_id"], ids.First()); Equal(1, ids.Length);
            Equal(1, handler.Calls.Count(ArtistCall)); Equal(3, handler.Calls.Count);
        });
        Test("MusicBee callback fetches Kien Romance with unchanged tags and no Custom10", () =>
        {
            var f = AliasRecording();
            var handler = new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("/api/artist/") ? f["details"].ToString() :
                r.RequestUri.AbsolutePath.Contains("lyric") ? Lyrics : f["search"].ToString()));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.TrackTitle ? (string)f["title"] :
                tag == Plugin.MetaDataType.Artist ? (string)f["artist"] : tag == Plugin.MetaDataType.Album ? (string)f["album"] : null, "3:41.736");
            Equal("[00:01.00]Hello", Lookup(plugin));
            Equal(1, handler.Calls.Count(ArtistCall)); Equal(1, handler.Calls.Count(c => c.Contains("lyric")));
        });
        Test("Ordinary exact artist matches make no alias requests", () =>
        {
            var handler = AliasFixture(new[] { AliasSong(artist: "Singer") });
            Equal(1L, AliasLookup(handler).Single()); Equal(1, handler.Calls.Count);
        });
        Test("Known Custom10 ID makes no artist metadata requests", () =>
        {
            var handler = AliasFixture(new[] { AliasSong() });
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
            Equal(0, handler.Calls.Count(ArtistCall));
        });
        foreach (var gate in new[] { "title", "album", "duration", "missing-duration", "missing-album", "version", "id" })
        Test("Artist alias fallback cannot bypass " + gate + " corroboration", () =>
        {
            var song = AliasSong();
            if (gate == "title") song.name = "Another Song";
            if (gate == "album") song.album.name = "Other Album";
            if (gate == "duration") song.duration = 203000;
            if (gate == "version") song.name = "Song (Live)";
            if (gate == "id") song.artists[0].id = 0;
            var handler = AliasFixture(new[] { song });
            Equal(0, AliasLookup(handler, album: gate == "missing-album" ? null : "Album", duration: gate == "missing-duration" ? (long?)null : 200000).Length);
            Equal(0, handler.Calls.Count(ArtistCall));
        });
        foreach (var response in new[] { "{\"code\":-460}", "{}", "null", "<html>bad</html>", AliasDetails(82, "Localized", "Singer"),
            AliasDetails(81, "Localized", "Someone else"), AliasDetails(81, "", "Singer"), "{\"code\":200,\"artist\":{\"id\":81,\"name\":\"Localized\",\"alias\":{}}}" })
        Test("Missing refused malformed or mismatched artist records cannot authorize a match", () =>
        {
            var handler = AliasFixture(new[] { AliasSong() }, response);
            Equal(0, AliasLookup(handler).Length); Equal(1, handler.Calls.Count(ArtistCall));
            True(handler.Calls.All(c => !c.StartsWith("GET http://")));
        });
        Test("Artist alias spelling is exact even when title typo matching is enabled", () =>
        {
            var handler = AliasFixture(new[] { AliasSong() }, AliasDetails(81, "Localized", "A Long Artist Name"));
            Equal(0, AliasLookup(handler, artist: "A Long Artist Namx").Length);
        });
        Test("A secondary composer alias cannot identify the primary singer", () =>
        {
            var song = AliasSong(); song.artists = new[] { new SearchResultArtist { id = 81, name = "Cover singer" },
                new SearchResultArtist { id = 82, name = "Localized" } };
            var handler = AliasFixture(new[] { song }, AliasDetails(81, "Cover singer", "Other"));
            Equal(0, AliasLookup(handler).Length);
            True(handler.Calls.Where(ArtistCall).All(c => c.EndsWith("/81")));
        });
        Test("Additional local artist credits must still agree when primary uses an alias", () =>
        {
            var song = AliasSong(); song.artists = song.artists.Concat(new[] { new SearchResultArtist { id = 82, name = "Guest" } }).ToArray();
            Equal(1L, AliasLookup(AliasFixture(new[] { song }), artist: "Singer;Guest").Single());
            Equal(0, AliasLookup(AliasFixture(new[] { song }), artist: "Singer;Other Guest").Length);
        });
        Test("Two different artists with the same verified alias remain ambiguous", () =>
        {
            var songs = new[] { AliasSong(), AliasSong(2, 82, "Localized Two") };
            var handler = new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("/api/artist/") ?
                AliasDetails(r.RequestUri.AbsolutePath.EndsWith("/81") ? 81 : 82, "Localized", "Singer") : SearchJson(songs)));
            Equal(0, AliasLookup(handler).Length); Equal(2, handler.Calls.Count(ArtistCall));
            True(handler.Calls.Count <= LookupSession.MaxSearchRequests);
        });
        Test("More plausible artist IDs than the alias budget are left unmatched", () =>
        {
            var handler = AliasFixture(new[] { AliasSong(), AliasSong(2, 82, "Two"), AliasSong(3, 83, "Three") });
            Equal(0, AliasLookup(handler).Length); Equal(0, handler.Calls.Count(ArtistCall));
        });
        Test("Different artist IDs with identical names cannot merge as alias duplicates", () =>
        {
            var songs = new[] { AliasSong(), AliasSong(2, 82) };
            var handler = new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("/api/artist/") ?
                AliasDetails(r.RequestUri.AbsolutePath.EndsWith("/81") ? 81 : 82, "Localized", "Singer") : SearchJson(songs)));
            Equal(0, AliasLookup(handler).Length); Equal(2, handler.Calls.Count(ArtistCall));
        });
        Test("Artist metadata requests share the search cap and have their own two-request cap", () =>
        {
            var handler = AliasFixture(new[] { AliasSong() });
            using (var session = Session(handler))
            {
                NeteaseApi.RequestArtistAliases(81, session); NeteaseApi.RequestArtistAliases(82, session);
                Equal(null, NeteaseApi.RequestArtistAliases(83, session));
                for (var i = 0; i < 15; i++) NeteaseApi.Search("Song " + i, session).ToArray();
            }
            Equal(2, handler.Calls.Count(ArtistCall)); Equal(LookupSession.MaxSearchRequests, handler.Calls.Count);
        });
        Test("Successful artist aliases are reused across lookups without requests", () =>
        {
            var cache = new ArtistAliasCache();
            var first = AliasFixture(new[] { AliasSong() }); var second = AliasFixture(new[] { AliasSong() });
            Equal(1L, AliasLookup(first, cache: cache).Single()); Equal(1L, AliasLookup(second, cache: cache).Single());
            Equal(1, first.Calls.Count(ArtistCall)); Equal(0, second.Calls.Count(ArtistCall));
        });
        Test("Failed artist metadata cools down across lookups then becomes retryable", () =>
        {
            var now = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc); var cache = new ArtistAliasCache(() => now);
            var failed = AliasFixture(new[] { AliasSong() }, "{\"code\":-460}");
            Equal(0, AliasLookup(failed, cache: cache).Length); Equal(1, failed.Calls.Count(ArtistCall));
            var retry = AliasFixture(new[] { AliasSong() });
            Equal(0, AliasLookup(retry, cache: cache).Length); Equal(0, retry.Calls.Count(ArtistCall));
            now = now.AddMinutes(5);
            Equal(1L, AliasLookup(retry, cache: cache).Single()); Equal(1, retry.Calls.Count(ArtistCall));
        });
        Test("Cache TTL failure cooldown capacity and defensive copies are bounded", () =>
        {
            var now = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc); var cache = new ArtistAliasCache(() => now);
            var names = new[] { "Singer" }; cache.Store(1, names); names[0] = "Changed";
            True(cache.TryGet(1, out var copy)); Equal("Singer", copy[0]); copy[0] = "Changed";
            True(cache.TryGet(1, out copy)); Equal("Singer", copy[0]);
            now = now.AddHours(24); True(!cache.TryGet(1, out copy));
            cache.Store(2, null); True(cache.TryGet(2, out copy)); Equal(0, copy.Length);
            now = now.AddMinutes(5); True(!cache.TryGet(2, out copy));
            for (var i = 1; i <= ArtistAliasCache.MaximumArtists + 10; i++) cache.Store(i, new[] { "Singer" });
            Equal(ArtistAliasCache.MaximumArtists, cache.Count); True(!cache.TryGet(1, out copy));
            cache.Store(9999, Enumerable.Repeat(new string('x', 300), 50).Concat(Enumerable.Range(0, 50).Select(i => "Alias " + i)).ToArray());
            True(cache.TryGet(9999, out copy)); True(copy.Length <= 33 && copy.All(n => n.Length <= 256));
        });
        Test("Injected sessions never share production alias cache", () =>
        {
            using (var a = Session(AliasFixture(new[] { AliasSong() })))
            using (var b = Session(AliasFixture(new[] { AliasSong() })))
            { True(!ReferenceEquals(a.AliasCache, b.AliasCache)); True(!ReferenceEquals(a.AliasCache, ArtistAliasCache.Shared)); }
        });
        Test("Cancelled search phase cannot spend time fetching aliases", () =>
        {
            var handler = AliasFixture(new[] { AliasSong() });
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                using (var session = new LookupSession(handler: handler, searchCancellation: cancellation.Token))
                { Equal(null, NeteaseApi.RequestArtistAliases(81, session)); Equal(0, handler.Calls.Count); }
            }
        });
        Test("A cancelled alias request does not consume the lyric reserve or cancel lyric retrieval", () =>
        {
            var handler = AliasFixture(new[] { AliasSong() });
            using (var cancellation = new CancellationTokenSource())
            {
                var token = cancellation.Token;
                handler.BeforeResponse = (r, _, t) => { if (r.RequestUri.AbsolutePath.Contains("/api/artist/")) { cancellation.Cancel(); t.ThrowIfCancellationRequested(); } };
                using (var session = new LookupSession(handler: handler, requestCancellation: () => token))
                {
                    Equal(0, NeteaseApi.RequestArtistAliases(81, session).Length); token = CancellationToken.None;
                    Equal("[00:01.00]Hello", NeteaseApi.RequestLyric(101, session).lrc.lyric);
                }
            }
        });
        Test("Artist metadata redirects are not followed or downgraded to HTTP", () =>
        {
            var handler = new Fixture((r, _) => r.RequestUri.AbsolutePath.Contains("/api/artist/") ?
                new System.Net.Http.HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("http://other.example/artist") } } : Json(SearchJson(new[] { AliasSong() })));
            Equal(0, AliasLookup(handler).Length); Equal(1, handler.Calls.Count(ArtistCall));
            True(handler.Calls.All(c => !c.Contains("other.example") && !c.StartsWith("GET http://")));
        });
        Test("Alias diagnostics contain structured events without artist names", () =>
        {
            var handler = AliasFixture(new[] { AliasSong() }); var diagnostics = new DevelopmentDiagnostics();
            using (var session = Session(handler))
            {
                session.DiagnosticLookup = diagnostics.Begin(new NeteaseConfig());
                Equal(1L, SearchMatch.SearchCandidates("Song", "Singer", "Album", 200000, session).Single());
            }
            var report = diagnostics.Report(); True(report.Contains("ArtistAliasLookup") && report.Contains("ArtistAliasMatch"));
            True(!report.Contains("Localized") && !report.Contains("Singer"));
        });
        foreach (var refusal in new[] { "HTTP 404", "API -460" })
        Test("Optional artist " + refusal + " does not block permitted lyric HTTP fallback", () =>
        {
            foreach (var allowHttp in new[] { true, false })
            {
                var handler = new Fixture((r, _) =>
                {
                    if (r.RequestUri.AbsolutePath.Contains("/api/artist/"))
                        return refusal == "HTTP 404" ? new System.Net.Http.HttpResponseMessage(HttpStatusCode.NotFound) : Json("{\"code\":-460}");
                    if (r.RequestUri.Scheme == "https") throw new System.Net.Http.HttpRequestException("fixture HTTPS failure");
                    return Json(Lyrics);
                });
                using (var session = Session(handler))
                {
                    session.AllowInsecureHttp = allowHttp;
                    Equal(0, NeteaseApi.RequestArtistAliases(81, session).Length);
                    var lyrics = NeteaseApi.RequestLyric(101, session);
                    if (allowHttp) Equal("[00:01.00]Hello", lyrics.lrc.lyric); else Equal(null, lyrics);
                }
                Equal(allowHttp ? 1 : 0, handler.Calls.Count(c => c.StartsWith("GET http://")));
                True(handler.Calls.Where(ArtistCall).All(c => c.StartsWith("GET https://")));
            }
        });
        foreach (var failure in new[] { "cancellation", "transport", "server-error" })
        Test("Transient artist " + failure + " is retryable in the next lookup", () =>
        {
            var cache = new ArtistAliasCache();
            var failed = new Fixture((_, __) =>
            {
                if (failure == "cancellation") throw new OperationCanceledException();
                if (failure == "transport") throw new System.Net.Http.HttpRequestException("fixture network failure");
                return new System.Net.Http.HttpResponseMessage(HttpStatusCode.BadGateway);
            });
            using (var session = Session(failed))
            {
                session.AliasCache = cache; Equal(0, NeteaseApi.RequestArtistAliases(81, session).Length);
                True(!cache.TryGet(81, out _)); Equal(0, cache.Count);
            }
            var recovered = AliasFixture(new[] { AliasSong() });
            Equal(1L, AliasLookup(recovered, cache: cache).Single()); Equal(1, recovered.Calls.Count(ArtistCall));
        });
        Test("Artist reply size measures the entire UTF8 payload without retaining its text", () =>
        {
            var body = JObject.Parse(AliasDetails(81, "七音阿卡莉", "ナナヲアカリ"));
            body["hotSongs"] = new JArray(new JObject { ["name"] = new string('x', 4096) });
            var payload = body.ToString(); var handler = AliasFixture(new[] { AliasSong() }, payload);
            using (var session = Session(handler))
            {
                Equal(2, NeteaseApi.RequestArtistAliases(81, session).Length);
                Equal(System.Text.Encoding.UTF8.GetByteCount(payload), session.LastArtistReplyBytes);
                True(session.LastArtistReplyBytes > 4096);
            }
        });
        Test("Alias matches use the agreed trace without calling the artist identity an annotation", () =>
        {
            var trace = new System.Collections.Generic.List<string>(); var handler = AliasFixture(new[] { AliasSong() });
            using (var session = Session(handler, trace.Add))
                Equal(1L, SearchMatch.SearchCandidates("Song", "Singer", "Album", 200000, session).Single());
            True(trace.Contains("Matching: matched through a verified NetEase artist alias"));
            True(!trace.Any(s => s.Contains("artist alias annotation")));
        });
    }

    private static int LiveArtistAlias()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var f = AliasRecording();
        string expected;
        using (var session = new LookupSession(Console.WriteLine))
            expected = NeteaseApi.RequestLyric((long)f["correct_id"], session)?.lrc?.lyric;
        if (string.IsNullOrWhiteSpace(expected)) { Console.WriteLine("Reference lyrics unavailable"); return 1; }
        foreach (var spelling in new[] { true, false })
        {
            var api = Host(); var directory = NewTestDirectory();
            api.Setting_GetPersistentStoragePath = () => directory;
            api.Library_GetFileTag = (_, tag) => tag == Plugin.MetaDataType.TrackTitle ? (string)f["title"] :
                tag == Plugin.MetaDataType.Artist ? (string)f["artist"] : tag == Plugin.MetaDataType.Album ? (string)f["album"] : null;
            api.Library_GetFileProperty = (_, property) => property == Plugin.FilePropertyType.Duration ? "3:41.736" : null;
            LookupSession lastSession = null;
            var plugin = new Plugin(api, () => lastSession = new LookupSession(Console.WriteLine)); _directories[plugin] = directory;
            SetConfig(plugin, JsonConvert.SerializeObject(new NeteaseConfig { Format = NeteaseConfig.OutputFormat.Original, AllowSmallSpellingDifferences = spelling }));
            var text = Lookup(plugin, (string)f["artist"], (string)f["title"]);
            var correct = text == expected;
            Console.WriteLine("Live MusicBee callback: unchanged tags; Custom10 absent; spelling=" + spelling + "; correct original lyrics=" + correct);
            var artistId = (long)f["details"]["artist"]["id"];
            Console.WriteLine(lastSession != null && lastSession.LastArtistReplyBytes > 0 ?
                "/api/artist/" + artistId + " reply size: " + lastSession.LastArtistReplyBytes + " bytes" :
                "Artist aliases served from cache; no metadata reply downloaded");
            if (!correct) return 1;
        }
        Console.WriteLine("LIVE ARTIST ALIAS: passed; no audio sent or downloaded lyrics printed/saved");
        return 0;
    }
}
