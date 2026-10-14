using MusicBeePlugin;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Net.Http;

internal static partial class Program
{
    private static JObject SpinRecording()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NeteaseLyricsTest.Fixtures.SpinTheWheel.json"))
        using (var reader = new StreamReader(stream)) return JObject.Parse(reader.ReadToEnd());
    }
    private static SearchResultSong ShowSong(long id = 1, long duration = 200000) =>
        Song(id, "Song (from the series Distant Show)", "Distant Show", "Distant Show Original Soundtrack", duration);
    private static long ShowSelect(IEnumerable<SearchResultSong> songs, string title = "Song", string artist = "Singer", string album = "Distant Show Soundtrack", long? duration = 200000, string track = null) =>
        SearchMatch.SelectCandidate(songs, title, artist, album, duration, trackNumber: track);
    private static void ShowCreditedTests()
    {
        Test("Recorded Spin The Wheel shares one show group despite translated credit variants", () =>
        {
            var f = SpinRecording();
            var rows = f["queries"].ToArray();
            var songs = rows.SelectMany(row => row["songs"].ToObject<SearchResultSong[]>()).ToArray();
            foreach (var sequence in new[] { songs, songs.Reverse().ToArray() })
                Equal(2684031162L, ShowSelect(sequence, (string)f["title"], (string)f["artist"], (string)f["album"], (long)f["duration_ms"]));
        });
        Test("Recorded Spin The Wheel reaches lyrics and translation without new searches", () =>
        {
            var f = SpinRecording(); var rows = f["queries"].ToArray(); var searches = 0; var lyrics = 0;
            var handler = new Fixture((r, _) => {
                if (r.RequestUri.AbsolutePath.Contains("search")) return Json(SearchJson(rows[searches++]["songs"].ToObject<SearchResultSong[]>()));
                lyrics++;
                return Json(lyrics == 1 ? Lyrics : "{\"code\":200,\"lrc\":{\"lyric\":\"[00:01.00]Hello\"},\"tlyric\":{\"lyric\":\"[00:01.00]Translation\"}}");
            });
            var plugin = CreatePlugin(handler, duration: "2:35.773");
            var result = Lookup(plugin, (string)f["artist"], (string)f["title"], (string)f["album"]);
            True(result.Contains("Translation")); Equal(6, searches); Equal(2, lyrics); Equal(8, handler.Calls.Count);
            True(!handler.Calls.Any(ArtistCall));
        });
        Test("Show matching needs a local work link or an exact album alias", () =>
        {
            var song = ShowSong(); Equal(1L, ShowSelect(new[] { song }));
            Equal(0L, ShowSelect(new[] { song }, album: "Other Show Soundtrack"));
            Equal(0L, ShowSelect(new[] { song }, album: LocalSoundtrack));
            song.album.alias = new[] { LocalSoundtrack }; Equal(1L, ShowSelect(new[] { song }, album: LocalSoundtrack));
        });
        foreach (var artist in new[] { "Other Singer", "Distant Show;Other Singer", "AB", "A", "一" })
        Test("Show matching rejects personal extra or short credits: " + artist, () =>
        {
            var song = ShowSong(); song.artists = artist.Split(';').Select(a => new SearchResultArtist { name = a }).ToArray();
            Equal(0L, ShowSelect(new[] { song }));
        });
        foreach (var artists in new[] {
            new[] { new SearchResultArtist { name = "Distant Show" }, new SearchResultArtist { name = "" } },
            new[] { new SearchResultArtist { name = "Distant Show" }, null }, new SearchResultArtist[0] })
        Test("Every show credit must be present and usable " + artists.Length, () =>
        { var song = ShowSong(); song.artists = artists; Equal(0L, ShowSelect(new[] { song })); });
        foreach (var title in new[] { "Song", "Song Singer", "Song (from Distant Show)", "Another Song (from the series Distant Show)",
            "Song (from the series Distant Show - Remix)", "Song (from the series Distant Show - demo version)" })
        Test("Show matching rejects absent changed or versioned attribution: " + title, () =>
        { var song = ShowSong(); song.name = title; Equal(0L, ShowSelect(new[] { song })); });
        foreach (var version in new[] { "Live", "Remix", "Demo", "Piano", "Instrumental", "Sped Up", "TV Size" })
        Test("Show matching retains recording labels in either album: " + version, () =>
        {
            var song = ShowSong(); song.album.name += " " + version; Equal(0L, ShowSelect(new[] { song }));
            Equal(0L, ShowSelect(new[] { ShowSong() }, album: "Distant Show Soundtrack " + version));
        });
        foreach (var duration in new long?[] { null, 0, 197999, 202001 })
        Test("Show matching requires a known close duration: " + duration, () =>
            Equal(0L, ShowSelect(new[] { ShowSong() }, duration: duration)));
        Test("Show duration guard includes both two-second boundaries", () =>
        { Equal(1L, ShowSelect(new[] { ShowSong() }, duration: 198000)); Equal(1L, ShowSelect(new[] { ShowSong() }, duration: 202000)); });
        Test("Two-character CJK work credits meet the minimum length", () =>
            Equal(1L, ShowSelect(new[] { Song(1, "Song (from the film 远方)", "远方", "远方 Original Soundtrack", 200000) }, album: "远方 原声带")));
        Test("Show matching needs a singer and a soundtrack album", () =>
        {
            Equal(0L, ShowSelect(new[] { ShowSong() }, artist: ""));
            Equal(0L, ShowSelect(new[] { ShowSong() }, album: ""));
            var song = ShowSong(); song.album.name = "Distant Show"; Equal(0L, ShowSelect(new[] { song }));
        });
        Test("Two attributed works reject the entire show tier in either result order", () =>
        {
            var songs = new[] { ShowSong(), Song(2, "Song (from the series Other Show)", "Other Show", "Other Show Soundtrack", 201000) };
            Equal(0L, ShowSelect(songs, album: "Distant Show Other Show Soundtrack"));
            Equal(0L, ShowSelect(songs.Reverse(), album: "Distant Show Other Show Soundtrack"));
        });
        Test("Show grouping cannot bridge duration differences", () =>
        {
            var songs = new[] { ShowSong(1, 198000), ShowSong(2, 200000), ShowSong(3, 202000) };
            Equal(0L, ShowSelect(songs)); Equal(0L, ShowSelect(songs.Reverse()));
        });
        Test("Show matching does not combine with a track prefix or artist alias", () =>
        {
            Equal(0L, ShowSelect(new[] { ShowSong() }, title: "01 Song", track: "1"));
            var song = ShowSong(); song.artists = new[] { new SearchResultArtist { id = 81, name = "Different Singer" } };
            var handler = AliasFixture(new[] { song }, AliasDetails(81, "Different Singer", "Singer"));
            Equal(0, AliasLookup(handler, album: "Distant Show Soundtrack").Length);
            Equal(0, handler.Calls.Count(ArtistCall));
        });
        Test("Ordinary candidates including ambiguity decide before the show tier", () =>
        {
            var songs = new[] { Song(8, "Song", "Singer", "Distant Show Soundtrack", 200000), ShowSong() };
            Equal(8L, ShowSelect(songs));
            Equal(0L, ShowSelect(songs.Concat(new[] { Song(9, "Song", "Singer", "Distant Show Soundtrack", 203000) })));
        });
        Test("Show artist already in local credits uses the earlier matching rules", () =>
        {
            var trace = new List<string>(); var diagnostics = new DevelopmentDiagnostics();
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { ShowSong() })));
            using (var session = Session(handler, trace.Add))
            {
                session.DiagnosticLookup = diagnostics.Begin(new NeteaseConfig());
                Equal(1L, SearchMatch.SearchCandidates("Song", "Distant Show", "Distant Show Soundtrack", 200000, session).Single());
            }
            True(diagnostics.Report().Contains("OrdinaryMatch")); True(!diagnostics.Report().Contains("ShowCreditedSoundtrackMatch"));
        });
        Test("Selected show tier has its own trace and private diagnostic event", () =>
        {
            var trace = new List<string>(); var diagnostics = new DevelopmentDiagnostics();
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { ShowSong() })));
            using (var session = Session(handler, trace.Add))
            {
                session.DiagnosticLookup = diagnostics.Begin(new NeteaseConfig());
                Equal(1L, SearchMatch.SearchCandidates("Song", "Singer", "Distant Show Soundtrack", 200000, session).Single());
            }
            True(trace.Contains("Matching: matched a soundtrack credited to the show"));
            True(diagnostics.Report().Contains("ShowCreditedSoundtrackMatch"));
            True(!diagnostics.Report().Contains("Distant Show") && !diagnostics.Report().Contains("Singer"));
        });
    }
}
