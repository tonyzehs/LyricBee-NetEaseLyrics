using MusicBeePlugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

internal static partial class Program
{
    private const string BrokenCreditOnly = "[00:00.00-1]作词 : Example\n[00:00.00-1]作曲 : Example";
    private const string JsonCreditOnly = "{\"t\":-1,\"c\":[{\"tx\":\"作曲: \"},{\"tx\":\"Wolfgang Amadeus Mozart\",\"li\":\"https://example.invalid/art\",\"or\":\"orpheus://nm/artist/home?id=1\"}]}";
    private static string LyricBody(string text) => JsonConvert.SerializeObject(new { code = 200, lrc = new { lyric = text } });

    private static SearchResultSong AlbumAnchor(long id = 10, long duration = 200000)
    {
        var song = ShowSong(id, duration);
        song.album.name = "Distant Show Studio Original Soundtrack";
        song.artists = new[] { new SearchResultArtist { id = 801, name = "Distant Show" },
            new SearchResultArtist { id = 802, name = "Show Studio" } };
        return song;
    }
    private static SearchResultSong AlbumPeer(long id = 20, long duration = 200000)
    {
        var song = AlbumAnchor(id, duration);
        song.artists[1] = new SearchResultArtist { id = 900, name = "远方制作" };
        return song;
    }
    private static long[] AlbumPeerIds(IEnumerable<SearchResultSong> songs)
    {
        var fixture = new Fixture((_, __) => Json(SearchJson(songs)));
        using (var session = Session(fixture))
            return SearchMatch.SearchCandidates("Song", "Singer", "Distant Show Soundtrack", 200000, session);
    }

    private static void CreditOnlyAndAlbumPeerTests()
    {
        foreach (var text in new[] { BrokenCreditOnly, JsonCreditOnly,
            "[00:60.00]Composer: Example", "[00:-1.00]Lyricist: Example", "[00:00.00][00:00.00-1]Vocals: Example" })
        Test("Credit-only text is unavailable even with broken timing or JSON: " + text.Length, () =>
        {
            True(!LyricProcessor.HasLyricText(text));
            var handler = new Fixture((_, __) => Json(LyricBody(text)));
            Equal(null, Lookup(CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
            Equal(1, handler.Calls.Count);
        });

        foreach (var text in new[] { "Hi", "[00:60.00]Real words", "[00:00.00-1]Real words",
            BrokenCreditOnly + "\n[00:01.00]Real words", JsonCreditOnly + "\nReal words",
            "{Actual words in braces}", "{\"t\":-1,\"c\":[{\"tx\":\"Real words\"}]}",
            "{\"t\":-1,\"c\":[{\"tx\":\"Composer: Example\\nReal words\"}]}",
            "{\"t\":\"-1\",\"c\":[{\"tx\":\"Composer: Example\"}]}",
            "{\"t\":-1,\"c\":[{\"tx\":\"Composer: Example\"}],\"lyrics\":\"Real words\"}",
            "{\"t\":-1,\"c\":[{\"tx\":\"Composer: Example\"},{\"tx\":\"Other\",\"lyrics\":\"Real words\"}]}",
            "{\"t\":-1,\"c\":[{\"tx\":\"Composer: Example\"}],\"t\":0}",
            "{\"t\":-1,\"c\":[{\"tx\":\"Composer: Example\"}]} Real words" })
        Test("Real short broken or unfamiliar structured text is retained: " + text.Length, () =>
        {
            True(LyricProcessor.HasLyricText(text));
            var handler = new Fixture((_, __) => Json(LyricBody(text)));
            Equal(text, Lookup(CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
        });

        Test("Two credit-only listings leave the full missing-original retry budget available", () =>
        {
            var lyrics = 0;
            var handler = new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("search") ?
                SearchJson(new[] { Song(1, "Song", "Singer", "Album", 200000), Song(2, "Song", "Singer", "Album", 200000), Song(3, "Song", "Singer", "Album", 200000) }) :
                ++lyrics == 1 ? LyricBody(BrokenCreditOnly) : lyrics == 2 ? LyricBody(JsonCreditOnly) : Lyrics));
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler, duration: "3:20"), "Singer", "Song", "Album"));
            Equal(3, lyrics); Equal(4, handler.Calls.Count);
        });
        Test("A saved ID with only JSON credits falls through to ordinary matching", () =>
        {
            var lyrics = 0;
            var handler = new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("search") ? Search : ++lyrics == 1 ? LyricBody(JsonCreditOnly) : Lyrics));
            Equal("[00:01.00]Hello", Lookup(SavedPlugin(handler, SavedComment()), "Singer", "Song", "Album"));
            Equal(3, handler.Calls.Count);
        });
        Test("Genuine untimed originals keep the existing preference when another original has timing", () =>
        {
            var lyrics = 0;
            var handler = new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("search") ?
                SearchJson(new[] { Song(1, "Song", "Singer", "Album", 200000), Song(2, "Song", "Singer", "Album", 200000) }) :
                ++lyrics == 1 ? LyricBody("Real untimed words") : Lyrics));
            Equal("Real untimed words", Lookup(CreatePlugin(handler, duration: "3:20"), "Singer", "Song", "Album")); Equal(2, lyrics);
        });

        Test("A same-album entry joins only through a fully qualified direct anchor", () =>
        {
            var songs = new[] { AlbumAnchor(), AlbumPeer() };
            Equal("10,20", string.Join(",", AlbumPeerIds(songs)));
            Equal("10,20", string.Join(",", AlbumPeerIds(songs.Reverse())));
            Equal(0, AlbumPeerIds(new[] { AlbumPeer() }).Length);
        });
        var rejectionCase = 0;
        foreach (var change in new Action<SearchResultSong>[] {
            s => s.name = "Another Song (from the series Distant Show)",
            s => s.name = "Song (from the film Distant Show)",
            s => s.name = "Song (from the series Other Show)",
            s => s.name = "Song (Live) (from the series Distant Show)",
            s => s.album.name += "!", s => s.album.name += " Live", s => s.album.name = "Other Show Original Soundtrack",
            s => s.duration = 202001, s => s.duration = 0,
            s => s.artists[0].id = 0, s => s.artists[0].id = 999,
            s => s.artists[1].name = "", s => s.artists[1] = null,
            s => s.artists[1].name = "AB", s => s.artists[1].name = "一",
            s => s.artists = s.artists.Concat(new[] { new SearchResultArtist { id = 77, name = "Other Singer" } }).ToArray() })
        Test("Same-album admission rejects conflicting missing or additional evidence " + ++rejectionCase, () =>
        {
            var peer = AlbumPeer(); change(peer);
            Equal("10", string.Join(",", AlbumPeerIds(new[] { AlbumAnchor(), peer })));
        });
        Test("New album peers cannot create an artist-ID chain", () =>
        {
            var a = AlbumAnchor(); var b = AlbumPeer(); var c = AlbumPeer(30);
            c.artists = new[] { new SearchResultArtist { id = 900, name = "远方制作" }, new SearchResultArtist { id = 901, name = "另一个名称" } };
            Equal("10,20", string.Join(",", AlbumPeerIds(new[] { a, b, c })));
            Equal("10,20", string.Join(",", AlbumPeerIds(new[] { c, b, a })));
        });
        Test("New album peers cannot bridge a four-second duration difference", () =>
            Equal("20,10", string.Join(",", AlbumPeerIds(new[] { AlbumAnchor(duration: 198000), AlbumPeer(), AlbumPeer(30, 202000) }))));
        Test("Qualified conflicting works still stop the expanded show group", () =>
        {
            var other = Song(30, "Song (from the series Other Show)", "Other Show", "Other Show Original Soundtrack", 200000);
            Equal(0L, ShowSelect(new[] { AlbumAnchor(), AlbumPeer(), other }, album: "Distant Show Other Show Soundtrack"));
        });
        Test("A new album peer and a conflicting qualified duration still stop the lookup", () =>
            Equal(0, AlbumPeerIds(new[] { AlbumAnchor(10, 198000), AlbumAnchor(11, 202000), AlbumPeer() }).Length));

        Test("Recorded English-album Spin The Wheel skips credits and returns the compatible timed original", () =>
        {
            var f = SpinRecording(); var rows = f["queries"].ToArray(); var searches = 0; var lyrics = 0;
            var handler = new Fixture((r, _) => {
                if (r.RequestUri.AbsolutePath.Contains("search")) return Json(SearchJson(rows[searches++]["songs"].ToObject<SearchResultSong[]>()));
                return Json(++lyrics == 1 ? LyricBody(BrokenCreditOnly) : Lyrics);
            });
            var plugin = CreatePlugin(handler, duration: "2:35.773");
            Equal("[00:01.00]Hello", Lookup(plugin, "Mick Wingert", "Spin The Wheel", "Arcane League of Legends: Season 2 (Soundtrack from the Animated Series)"));
            Equal(6, searches); Equal(2, lyrics); Equal(8, handler.Calls.Count); True(!handler.Calls.Any(ArtistCall));
            var field = typeof(Plugin).GetField("_diagnostics", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var report = ((DevelopmentDiagnostics)field.GetValue(plugin)).Report();
            True(report.Contains("OriginalMissing") && report.Contains("SoundtrackAlbumPeer"));
            True(report.Contains("song_id=2684869167") && report.Contains("song_id=3374505658"));
            True(!report.Contains("Mick Wingert") && !report.Contains("Arcane"));
        });
    }
}
