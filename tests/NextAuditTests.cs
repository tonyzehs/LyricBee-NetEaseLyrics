using MusicBeePlugin;
using System;
using System.Linq;

internal static partial class Program
{
    private static void NextAuditTests()
    {
        Test("LB1952-001 malformed text follows the first occurrence of a compressed repeat", () =>
        {
            foreach (var separate in new[] { true, false })
            {
                var join = separate ? "\n" : " / ";
                Equal("[00:10.000]chorus" + join + "p1" + join + "t1\nloose\n[00:30.000]next" + join +
                    "p2" + join + "t2\n[01:20.000]chorus" + join + "p3" + join + "t3",
                    LyricProcessor.MergePronunciation("[00:10][01:20]chorus\n[00:60]loose\n[00:30]next",
                        "[00:10]p1\n[00:30]p2\n[01:20]p3", "[00:10]t1\n[00:30]t2\n[01:20]t3",
                        separate, null, out var used)); Equal(2, used);
            }
            Equal("[00:10.000]chorus\nt1\nloose\n[00:30.000]next\nt2\n[01:20.000]chorus\nt3",
                LyricProcessor.InjectTranslationTwoLines("[01:20][00:10]chorus\n[00:60]loose\n[00:30]next",
                    "[00:10]t1\n[00:30]t2\n[01:20]t3"));
        });
        Test("LB1952-002 whitespace after malformed text adds no empty continuation", () =>
        {
            Equal("[00:10.000]one\np1\nt1\nloose\ncontinued\n[00:14.000]three\np3\nt3",
                LyricProcessor.MergePronunciation("[00:10]one\n[00:60]loose\n\n   \ncontinued\n\n[00:14]three",
                    "[00:10]p1\n[00:14]p3", "[00:10]t1\n[00:14]t3", true, null, out var used));
            Equal(2, used);
        });
        Test("LB1952-003 differing valid lowercase Roman tokens preserve numbered identity", () =>
        {
            foreach (var pair in new[] { new[] { "vii", "viii" }, new[] { "ii", "iii" }, new[] { "xiv", "xvi" } })
                Equal(0L, Select(new[] { Song(1, "Symphony " + pair[1], "Singer", "Album", 200000) }, "Symphony " + pair[0]));
            Equal(1L, Select(new[] { Song(1, "Symphony VII", "Singer", "Album", 200000) }, "Symphony vii"));
            Equal(1L, Select(new[] { Song(1, "Mixx It Up Tonight", "Singer", "Album", 200000) }, "Mix It Up Tonight"));
        });
        Test("LB1952-004 unpadded title numbers cannot disappear when the full title is absent", () =>
        {
            foreach (var example in new[] { new[] { "7 Rings", "Rings", "7" }, new[] { "99 Problems", "Problems", "99" },
                new[] { "1 2 Step", "2 Step", "1" } })
            {
                Equal(0L, SearchMatch.SelectCandidate(new[] { Song(1, example[1], "Singer", "Album", 200000) },
                    example[0], "Singer", "Album", 200000, trackNumber: example[2]));
                var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ?
                    SearchJson(new[] { Song(1, example[1], "Singer", "Album", 200000) }) : Lyrics));
                var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.TrackNo ? example[2] : null, duration: "3:20");
                Equal(null, Lookup(plugin, "Singer", example[0], "Album"));
                Equal(6, handler.Calls.Count); True(handler.Calls.All(c => c.Contains("search")));
            }
        });
        Test("A stripped track prefix requires close duration full artists and the supplied album", () =>
        {
            var song = Song(1, "Lemon", "Singer", "Album", 200000);
            foreach (var title in new[] { "01 Lemon", "01. Lemon", "1 - Lemon" })
            {
                Equal(1L, SearchMatch.SelectCandidate(new[] { song }, title, "Singer", "Album", 202000, trackNumber: "1"));
                Equal(0L, SearchMatch.SelectCandidate(new[] { song }, title, "Singer", "Album", 202001, trackNumber: "1"));
                Equal(0L, SearchMatch.SelectCandidate(new[] { song }, title, "Singer", "Album", null, trackNumber: "1"));
                Equal(0L, SearchMatch.SelectCandidate(new[] { song }, title, "Singer", "Other", 200000, trackNumber: "1"));
                Equal(0L, SearchMatch.SelectCandidate(new[] { song }, title, "Singer;Guest", "Album", 200000, trackNumber: "1"));
                Equal(1L, SearchMatch.SelectCandidate(new[] { song }, title, "Singer", null, 200000, trackNumber: "1"));
            }
        });
        Test("Track prefixes use ordinary queries first then at most one cleaned query", () =>
        {
            var empty = SearchJson(new SearchResultSong[0]);
            var handler = new Fixture((request, number) => Json(request.RequestUri.AbsolutePath.Contains("search") ?
                (number <= 6 ? empty : SearchJson(new[] { Song(1, "Lemon", "Singer", "Album", 200000) })) : Lyrics));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.TrackNo ? "1" : null, duration: "3:20");
            SetConfig(plugin, "{\"Format\":0}");
            Equal("[00:01.00]Hello", Lookup(plugin, "Singer", "01 Lemon", "Album"));
            Equal(7, handler.Calls.Count(c => c.Contains("search")));
            Equal(1, handler.Calls.Count(c => c.Contains("lyric")));
        });
    }
}
