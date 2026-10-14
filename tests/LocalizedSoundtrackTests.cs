using MusicBeePlugin;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net.Http;

internal static partial class Program
{
    private const string LocalSoundtrack = "《远方的电影》原声带";
    private const string ForeignSoundtrack = "Distant Film Original Soundtrack";
    private const string ForeignAttribution = " (from the film Distant Film)";

    private sealed class LocalizedRecordings { public RecordingFixture[] cases { get; set; } }
    private static RecordingFixture[] ReadLocalizedRecordings()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NeteaseLyricsTest.Fixtures.LocalizedSoundtracks.json"))
        using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
            return JsonConvert.DeserializeObject<LocalizedRecordings>(reader.ReadToEnd()).cases;
    }
    private static SearchResultSong LocalizedSong(long id = 1, long duration = 200000) =>
        Song(id, "Song" + ForeignAttribution, "Singer", ForeignSoundtrack, duration);

    private static void LocalizedSoundtrackTests()
    {
        foreach (var f in ReadLocalizedRecordings())
        {
            Test("Recorded localized soundtrack selects " + f.title + " without retagging", () =>
            {
                foreach (var typos in new[] { false, true })
                foreach (var songs in new[] { f.observed, f.observed.Reverse().ToArray() })
                    Equal(f.correct.id, SearchMatch.SelectCandidate(songs, f.title, f.artist, f.album, f.duration_ms, typos));
            });
            Test("Recorded localized soundtrack host lookup bounds requests for " + f.title, () =>
            {
                var handler = new Fixture((request, _) =>
                {
                    if (request.RequestUri.AbsolutePath.Contains("search"))
                        return Json(SearchJson(request.Method == HttpMethod.Post ? f.observed : f.observed.Take(6)));
                    // Force the GET lyric fallback so the selected ID is inspectable without decrypting requests.
                    if (request.Method == HttpMethod.Post) return Json("{\"code\":200}");
                    True(request.RequestUri.Query.Contains("id=" + f.correct.id));
                    return Json(Lyrics);
                });
                var plugin = CreatePlugin(handler, duration: TimeSpan.FromMilliseconds(f.duration_ms).ToString(@"m\:ss\.fff"));
                SetConfig(plugin, "{\"Format\":0}");
                Equal("[00:01.00]Hello", Lookup(plugin, f.artist, f.title, f.album));
                Equal(6, handler.Calls.Count(c => c.Contains("search")));
                Equal(2, handler.Calls.Count(c => c.Contains("lyric")));
                Equal(8, handler.Calls.Count);
            });
        }
        foreach (var album in new[] { LocalSoundtrack, "遠い映画 オリジナル・サウンドトラック", "먼 영화 OST" })
        Test("Different-script soundtrack fallback supports " + album, () =>
            Equal(1L, Select(new[] { LocalizedSong() }, album: album)));

        foreach (var field in new[] { "alias", "tns", "transNames" })
        Test("Supplied album " + field + " can corroborate a translated soundtrack", () =>
        {
            const string translated = "La Película Lejana";
            var song = LocalizedSong();
            typeof(SearchResultAlbum).GetField(field).SetValue(song.album, new[] { translated });
            Equal(1L, Select(new[] { song }, album: translated));
            Equal(0L, Select(new[] { song }, album: "La Película Lejana 2"));
            // Exercise deserialization through the same response model used by the real interfaces.
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { song })));
            using (var session = Session(handler))
                Equal(1L, SearchMatch.SearchCandidates("Song", "Singer", translated, 200000, session).Single());
        });
        foreach (var album in new[] { "Other Movie Soundtrack", "Another Film 2 Soundtrack", "Other Album", "", null,
            "另一部电影", "另一部电影 Wrong Movie 原声带", "影片 A 原声带", "原声带", "サウンドトラック", "사운드트랙" })
        Test("Uncorroborated album stays rejected: " + album, () =>
            Equal(0L, Select(new[] { LocalizedSong() }, album: album)));

        foreach (var duration in new long?[] { null, 0, 197999, 202001 })
        Test("Localized attribution rejects unknown or distant duration " + duration, () =>
            Equal(0L, Select(new[] { LocalizedSong() }, album: LocalSoundtrack, duration: duration)));
        Test("Localized attribution duration boundary is inclusive", () =>
        {
            Equal(1L, Select(new[] { LocalizedSong() }, album: LocalSoundtrack, duration: 198000));
            Equal(1L, Select(new[] { LocalizedSong() }, album: LocalSoundtrack, duration: 202000));
            Equal(0L, Select(new[] { LocalizedSong(duration: 0) }, album: LocalSoundtrack));
        });
        foreach (var artist in new[] { null, "Other Singer", "Singeer", "Singer;Guest" })
        Test("Localized attribution requires exact full artist credits: " + artist, () =>
            Equal(0L, Select(new[] { LocalizedSong() }, artist: artist, album: LocalSoundtrack)));
        Test("Localized attribution cannot borrow a secondary performer or extra franchise credit", () =>
        {
            var song = LocalizedSong();
            song.artists = new[] { new SearchResultArtist { name = "Singer" }, new SearchResultArtist { name = "Guest" } };
            Equal(0L, Select(new[] { song }, album: LocalSoundtrack));
            Equal(0L, Select(new[] { song }, artist: "Guest", album: LocalSoundtrack));
            Equal(1L, Select(new[] { song }, artist: "Guest;Singer", album: LocalSoundtrack));
        });
        foreach (var title in new[] { "A Beautiful Sogn", "Theme Part 1", "Song (Live)", "Song (Remix)",
            "Song (demo version)", "Song (Piano Version)", "Song (Radio Edit)", "Song (Sped Up)" })
        Test("Localized attribution preserves title and recording distinctions: " + title, () =>
        {
            var song = Song(1, title + ForeignAttribution, "Singer", ForeignSoundtrack, 200000);
            Equal(0L, Select(new[] { song }, title == "A Beautiful Sogn" ? "A Beautiful Song" : title == "Theme Part 1" ? "Theme Part 2" : "Song", album: LocalSoundtrack));
        });
        foreach (var suffix in new[] { " (from Distant Film)", " (Distant Film soundtrack)",
            " (from the film Distant Film - Remix)", " (from the film Distant Film - demo version)" })
        Test("Localized fallback keeps unrecognized or versioned attribution " + suffix, () =>
            Equal(0L, Select(new[] { Song(1, "Song" + suffix, "Singer", ForeignSoundtrack + " Remix demo version", 200000) }, album: LocalSoundtrack)));
        Test("Foreign album must identify itself as a soundtrack and corroborate the named work", () =>
        {
            Equal(0L, Select(new[] { Song(1, "Song" + ForeignAttribution, "Singer", "Distant Film", 200000) }, album: LocalSoundtrack));
            Equal(0L, Select(new[] { Song(1, "Song" + ForeignAttribution, "Singer", "Other Film Soundtrack", 200000) }, album: LocalSoundtrack));
        });
        foreach (var version in new[] { "Live", "Remix", "demo", "Piano", "Radio Edit" })
        Test("Localized fallback retains an album-only recording label: " + version, () =>
        {
            var song = LocalizedSong(); song.album.name += " (" + version + ")";
            Equal(0L, Select(new[] { song }, album: LocalSoundtrack));
            Equal(0L, Select(new[] { LocalizedSong() }, album: LocalSoundtrack + " (" + version + ")"));
        });
        Test("Conflicting attributions remain rejected", () =>
            Equal(0L, Select(new[] { LocalizedSong() }, "Song (from the film Other Film)", album: LocalSoundtrack)));
        Test("Competing foreign works reject the localized fallback in either response order", () =>
        {
            var songs = new[] { LocalizedSong(), Song(2, "Song (from the film Another Film)", "Singer", "Another Film Original Soundtrack", 201999) };
            Equal(0L, Select(songs, album: LocalSoundtrack));
            Equal(0L, Select(songs.Reverse(), album: LocalSoundtrack));
        });
        Test("Equivalent localized duplicates retain deterministic grouping", () =>
        {
            var songs = new[] { LocalizedSong(2, 201000), LocalizedSong(1, 200000) };
            Equal(1L, Select(songs, album: LocalSoundtrack));
            Equal(1L, Select(songs.Reverse(), album: LocalSoundtrack));
        });
        Test("Ordinary matching remains preferred and does not add requests", () =>
        {
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { LocalizedSong(), Song(9, "Song", "Singer", "Album", 200000) })));
            using (var session = Session(handler)) Equal(9L, SearchMatch.SearchCandidates("Song", "Singer", "Album", 200000, session).Single());
            Equal(1, handler.Calls.Count);
        });
        Test("Ordinary ambiguity cannot be bypassed by a localized candidate", () =>
            Equal(0L, Select(new[] { Song(8, "Song", "Singer", "Album", 200000), Song(9, "Song", "Singer", "Album", 203000), LocalizedSong() }, album: LocalSoundtrack)));
        Test("Localized fallback does not combine with TrackNo prefix removal", () =>
            Equal(0L, SearchMatch.SelectCandidate(new[] { LocalizedSong() }, "01 Song", "Singer", LocalSoundtrack, 200000, trackNumber: "1")));
        Test("Localized fallback does not combine with artist-alias lookup", () =>
        {
            var song = LocalizedSong(); song.artists[0].name = "Localized Singer"; song.artists[0].id = 81;
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { song })));
            using (var session = Session(handler)) Equal(0, SearchMatch.SearchCandidates("Song", "Singer", LocalSoundtrack, 200000, session).Length);
            Equal(0, handler.Calls.Count(c => c.Contains("/api/artist/")));
        });
        Test("Localized fallback diagnostics identify the rule without copying metadata", () =>
        {
            var trace = new System.Collections.Generic.List<string>();
            var diagnostics = new DevelopmentDiagnostics();
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { LocalizedSong() })));
            using (var session = Session(handler, trace.Add))
            {
                session.DiagnosticLookup = diagnostics.Begin(new NeteaseConfig());
                Equal(1L, SearchMatch.SearchCandidates("Song", "Singer", LocalSoundtrack, 200000, session).Single());
            }
            True(trace.Contains("Matching: used restricted localized soundtrack fallback"));
            True(diagnostics.Report().Contains("LocalizedSoundtrackMatch"));
            True(!diagnostics.Report().Contains("Distant Film") && !diagnostics.Report().Contains(LocalSoundtrack));
        });
    }
}
