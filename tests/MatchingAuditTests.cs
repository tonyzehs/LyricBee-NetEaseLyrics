using MusicBeePlugin;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

internal static partial class Program
{
    private static void MatchingAuditTests()
    {
        Test("Numeric title counterexamples reject another part or numbered song", () =>
        {
            foreach (var pair in new[] { new[] { "Theme of Love Part 1", "Theme of Love Part 2" },
                new[] { "Kimi ni Todoke No.5", "Kimi ni Todoke No.6" },
                new[] { "A Beautiful Song 12", "A Beautiful Song 13" },
                new[] { "A Beautiful Song 1 23", "A Beautiful Song 12 3" },
                new[] { "A Beautiful Song", "A Beautiful Song 1" } })
                Equal(0L, Select(new[] { Song(1, pair[1], "Singer", "Album", 200000) }, pair[0]));
        });
        Test("Explicit Roman part labels normalize to decimal part labels", () =>
        {
            foreach (var label in new[] { "Pt. II", "Part II", "Pt 2", "Part 02", "Ｐｔ． ＩＩ" })
                Equal(1L, Select(new[] { Song(1, "Theme of Love Part 2", "Singer", "Album", 200000) }, "Theme of Love " + label));
            Equal(0L, Select(new[] { Song(1, "Theme of Love Part III", "Singer", "Album", 200000) }, "Theme of Love Pt. II"));
            Equal(0L, Select(new[] { Song(1, "Theme of Love Part 4", "Singer", "Album", 200000) }, "Theme of Love Pt. IIII"));
        });
        Test("Ordinary Roman-looking words remain words", () =>
            Equal(1L, Select(new[] { Song(1, "I Live in Rome", "Singer", "Album", 200000) }, "I Live in Rome")));
        Test("Standalone Roman numerals cannot be changed as spelling errors", () =>
        {
            Equal(0L, Select(new[] { Song(1, "A Beautiful Theme No. VI", "Singer", "Album", 200000) }, "A Beautiful Theme No. V"));
            Equal(0L, Select(new[] { Song(1, "A Beautiful Symphony VI", "Singer", "Album", 200000) }, "A Beautiful Symphony V"));
            Equal(1L, Select(new[] { Song(1, "A Beautiful Symphony v", "Singer", "Album", 200000) }, "A Beautiful Symphony V"));
            Equal(1L, Select(new[] { Song(1, "The Perfect mix", "Singer", "Album", 200000) }, "The Perfect MIX"));
            Equal(0L, Select(new[] { Song(1, "I Love This Beautiful Music", "Singer", "Album", 200000) }, "1 Love This Beautiful Music"));
        });
        Test("Recognized remaster annotations do not introduce title numbers", () =>
        {
            foreach (var suffix in new[] { " - Remastered 2009", " (Remastered 2009)", " (2009 Remaster)", " [Digitally Remastered 2009]" })
            {
                Equal(1L, Select(new[] { Song(1, "Yesterday", "Singer", "Album", 200000) }, "Yesterday" + suffix));
                Equal(1L, Select(new[] { Song(1, "Yesterday" + suffix, "Singer", "Album", 200000) }, "Yesterday", duration: null));
            }
            Equal(0L, Select(new[] { Song(1, "Theme Part 2", "Singer", "Album", 200000) }, "Theme Pt. I (2009 Remaster)"));
        });
        Test("Remaster is not removed from the substantive song title", () =>
            Equal(0L, Select(new[] { Song(1, "Remastered Love 2009", "Singer", "Album", 200000) }, "Love")));
        Test("Mono stereo and album-version notes require close known local duration", () =>
        {
            foreach (var suffix in new[] { " (Mono)", " - Stereo", " (Album Version)" })
            {
                Equal(1L, Select(new[] { Song(1, "Yesterday", "Singer", "Album", 201999) }, "Yesterday" + suffix));
                Equal(0L, Select(new[] { Song(1, "Yesterday", "Singer", "Album", 202001) }, "Yesterday" + suffix));
                Equal(0L, Select(new[] { Song(1, "Yesterday", "Singer", "Album", 200000) }, "Yesterday" + suffix, duration: null));
                Equal(0L, Select(new[] { Song(1, "Yesterday", "Singer", "Album", 0) }, "Yesterday" + suffix));
                Equal(1L, Select(new[] { Song(1, "Yesterday" + suffix, "Singer", "Album", 200000) }, "Yesterday" + suffix, duration: null));
            }
        });
        Test("Live radio edits remixes and original mixes remain meaningful", () =>
        {
            foreach (var suffix in new[] { " (Live)", " (Radio Edit)", " (Remix)", " (Original Mix)", " (Live - 2009 Remaster)" })
                Equal(0L, Select(new[] { Song(1, "Yesterday" + suffix, "Singer", "Album", 200000) }, "Yesterday"));
        });
        Test("Captured Jonas Blue duplicates resolve Blue and absent album deterministically", () =>
        {
            var songs = JonasBlueListings();
            Equal(3, songs.Length);
            foreach (var album in new[] { "Blue", null })
            {
                var chosen = Select(songs, "By Your Side", "Jonas Blue", album, 201053);
                True(songs.Any(s => s.id == chosen));
                Equal(chosen, Select(songs.Reverse(), "By Your Side", "Jonas Blue", album, 201053));
            }
            Equal(456370693L, Select(songs, "By Your Side", "Jonas Blue", "Crowd Pleasers", 201053));
        });
        Test("Duplicate ranking prefers album then duration then a stable ID", () =>
        {
            var songs = new[] { Song(1, "Song", "Singer", "One", 200000), Song(2, "Song", "Singer", "Two", 201000) };
            Equal(2L, Select(songs, album: "Two"));
            Equal(1L, Select(songs, album: null));
            songs[1].duration = 200000;
            Equal(1L, Select(songs.Reverse(), album: null));
            Equal(1L, Select(songs, album: null, duration: null));
        });
        Test("Different secondary credits do not form one duplicate group", () =>
        {
            var a = Song(1, "Song", "Singer", "One", 200000);
            var b = Song(2, "Song", "Singer", "Two", 200000);
            a.artists = a.artists.Concat(new[] { new SearchResultArtist { name = "Guest A" } }).ToArray();
            b.artists = b.artists.Concat(new[] { new SearchResultArtist { name = "Guest B" } }).ToArray();
            Equal(0L, Select(new[] { a, b }, album: null));
        });
        Test("Unknown service duration does not establish duplicate equivalence", () =>
            Equal(0L, Select(new[] { Song(1, "Song", "Singer", "One", 0), Song(2, "Song", "Singer", "Two", 0) }, album: null, duration: null)));
        Test("Duplicate duration span cannot bridge into a four-second group", () =>
            Equal(0L, Select(new[] { Song(1, "Song", "Singer", "One", 200000), Song(2, "Song", "Singer", "Two", 202000),
                Song(3, "Song", "Singer", "Three", 204000) }, album: null)));
        Test("Legacy spelling option resolves duplicates without discarding callback metadata", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search")
                ? SearchJson(JonasBlueListings()) : Lyrics));
            var plugin = CreatePlugin(handler);
            SetConfig(plugin, "{\"UseLegacyMatch\":true,\"Fuzzy\":false}");
            Equal("[00:01.00]Hello", Lookup(plugin, "Jonas Blue", "By Your Side", "Blue"));
        });
        Test("Duplicate and suffix decisions emit traces without raw song metadata", () =>
        {
            var trace = new List<string>();
            var handler = new Fixture((_, __) => Json(SearchJson(new[] {
                Song(1, "Yesterday", "Singer", "Secret Album", 200000), Song(2, "Yesterday", "Singer", "Other", 201000) })));
            using (var session = Session(handler, trace.Add))
                Equal(1L, SearchMatch.SearchAndMatch("Yesterday (2009 Remaster)", "Singer", "Secret Album", 200000, session));
            True(trace.Any(t => t.Contains("grouped 2")));
            True(trace.Any(t => t.Contains("remaster")));
            True(trace.All(t => !t.Contains("Secret Album") && !t.Contains("Yesterday")));
        });
        Test("A successful duplicate lookup adds no lyric comparisons", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search")
                ? SearchJson(JonasBlueListings()) : Lyrics));
            var plugin = CreatePlugin(handler);
            SetConfig(plugin, "{\"Format\":0}");
            Equal("[00:01.00]Hello", Lookup(plugin, "Jonas Blue", "By Your Side", "Blue"));
            Equal(3, handler.Calls.Count(c => c.Contains("search")));
            Equal(1, handler.Calls.Count(c => c.Contains("lyric")));
        });
        Test("Missing lyrics retry only compatible catalogue listings", () =>
        {
            var handler = new Fixture((request, number) => Json(request.RequestUri.AbsolutePath.Contains("search")
                ? SearchJson(TwoListings()) : number <= 3 ? "{\"code\":200}" : Lyrics));
            var plugin = CreatePlugin(handler);
            SetConfig(plugin, "{\"Format\":0}");
            Equal("[00:01.00]Hello", Lookup(plugin, "Singer", "Song", "Album"));
            Equal(1, handler.Calls.Count(c => c.Contains("search")));
            Equal(3, handler.Calls.Count(c => c.Contains("lyric")));
        });
        Test("Missing translation retries a compatible listing with its own original timing", () =>
        {
            var handler = new Fixture((request, number) => Json(request.RequestUri.AbsolutePath.Contains("search")
                ? SearchJson(TwoListings()) : number == 2 ? Lyrics :
                "{\"code\":200,\"lrc\":{\"lyric\":\"[00:02.00]Second\"},\"tlyric\":{\"lyric\":\"[00:02.00]Translation\"}}"));
            Equal("[00:02.000]Second\nTranslation", Lookup(CreatePlugin(handler), "Singer", "Song", "Album"));
            Equal(3, handler.Calls.Count);
        });
        Test("Original-only mode does not retry for missing translations", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? SearchJson(TwoListings()) : Lyrics));
            var plugin = CreatePlugin(handler);
            SetConfig(plugin, "{\"Format\":0}");
            Equal("[00:01.00]Hello", Lookup(plugin, "Singer", "Song", "Album"));
            Equal(2, handler.Calls.Count);
        });
        Test("Translation retries preserve the first usable original when alternatives fail", () =>
        {
            var handler = new Fixture((request, number) => Json(request.RequestUri.AbsolutePath.Contains("search")
                ? SearchJson(TwoListings()) : number == 2 ? Lyrics : "{\"code\":200}"));
            Equal("[00:01.00]Hello", Lookup(CreatePlugin(handler), "Singer", "Song", "Album"));
            True(handler.Calls.Count <= 5);
        });
        Test("Instrumental and Custom10 responses do not switch to another song", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search")
                ? SearchJson(TwoListings()) : "{\"code\":200,\"nolyric\":true}"));
            Equal(null, Lookup(CreatePlugin(handler), "Singer", "Song", "Album"));
            Equal(2, handler.Calls.Count);
            var overrideHandler = new Fixture((_, __) => Json("{\"code\":200}"));
            Equal(null, Lookup(CreatePlugin(overrideHandler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null)));
            Equal(2, overrideHandler.Calls.Count);
        });
        Test("Search request cap leaves a separate lyric request allowance", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? "{\"code\":-460}" : Lyrics));
            using (var session = Session(handler))
            {
                Equal(0L, SearchMatch.SearchAndMatch("Song", "One;Two;Three", "Album", 200000, session));
                Equal(LookupSession.MaxSearchRequests, handler.Calls.Count);
                Equal("[00:01.00]Hello", NeteaseApi.RequestLyric(101, session).lrc.lyric);
                Equal(LookupSession.MaxSearchRequests + 1, handler.Calls.Count);
            }
        });
        Test("Both phases enforce their request caps and total never exceeds eighteen", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":-460}"));
            using (var session = Session(handler))
            {
                SearchMatch.SearchAndMatch("Song", "One;Two;Three", "Album", 200000, session);
                for (var id = 1; id <= 20; id++) NeteaseApi.RequestLyric(id, session);
                Equal(LookupSession.MaxSearchRequests + LookupSession.MaxLyricRequests, handler.Calls.Count);
            }
        });
        Test("At most three compatible listings get lyric attempts", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search")
                ? SearchJson(Enumerable.Range(1, 10).Select(id => Song(id, "Song", "Singer", "Album", 200000))) : "{\"code\":200}"));
            Equal(null, Lookup(CreatePlugin(handler), "Singer", "Song", "Album"));
            Equal(1, handler.Calls.Count(c => c.Contains("search")));
            Equal(6, handler.Calls.Count(c => c.Contains("lyric")));
        });
    }

    private static SearchResultSong[] JonasBlueListings() => ReadRecordingFixture().observed
        .Where(s => s.name == "By Your Side" && s.artists.Any(a => a.name == "Jonas Blue")).ToArray();

    private static SearchResultSong[] TwoListings() => new[] {
        Song(1, "Song", "Singer", "Album", 200000), Song(2, "Song", "Singer", "Other album", 201000) };
}
