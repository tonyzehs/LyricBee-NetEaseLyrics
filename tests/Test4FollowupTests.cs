using MusicBeePlugin;
using System;
using System.Linq;

internal static partial class Program
{
    private static void Test4FollowupTests()
    {
        foreach (var separate in new[] { true, false })
        {
            Test("Broken credit prefixes are removed without altering timed lyric translation: " + separate, () =>
            {
                var join = separate ? "\n" : " / ";
                Equal("作词 : Example\n作曲 : Example\n[00:10.000]one" + join + "t1\n[00:15.000]two" + join + "t2",
                    LyricProcessor.MergePronunciation(BrokenCreditOnly + "\n[00:10]one\n[00:15]two", null,
                        "[00:10]t1\n[00:15]t2", separate, null, out var used));
                Equal(1, used);
            });
            Test("An interleaved JSON credit cannot take the preceding lyric translation: " + separate, () =>
            {
                var join = separate ? "\n" : " / ";
                Equal(JsonCreditOnly + "\n[00:10.000]one" + join + "t1\n[00:15.000]two" + join + "t2",
                    LyricProcessor.MergePronunciation("[00:10]one\n" + JsonCreditOnly + "\n[00:15]two", null,
                        "[00:10]t1\n[00:15]t2", separate, null, out var used));
                Equal(1, used);
            });
            foreach (var header in new[] { BrokenCreditOnly, JsonCreditOnly })
            Test("Untimed words following credits keep their paired translation: " + separate + "/" + header.Length, () =>
            {
                var displayedHeader = header == BrokenCreditOnly ? "作词 : Example\n作曲 : Example" : header;
                var expected = displayedHeader + "\none / t1\ntwo / t2";
                Equal(expected, LyricProcessor.MergePronunciation(header + "\none\ntwo", null, "t1\nt2", separate, null, out var used));
                Equal(1, used);
                Equal(expected, LyricProcessor.MergeTranslation(header + "\none\ntwo", "t1\nt2", separate));
            });
        }
        Test("Cleaned credits preserve original pronunciation and translation together", () =>
        {
            Equal("作词 : Example\n作曲 : Example\n[00:10.000]one\np1\nt1\n[00:15.000]two\np2\nt2",
                LyricProcessor.MergePronunciation(BrokenCreditOnly + "\n[00:10]one\n[00:15]two",
                    "[00:10]p1\n[00:15]p2", "[00:10]t1\n[00:15]t2", true, null, out var used));
            Equal(2, used);
        });

        foreach (var title in new[] { "01 Lemon", "01. Lemon", "1 - Lemon" })
        Test("Strong tagged track prefix finishes after one search: " + title, () =>
        {
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { Song(1, "Lemon", "Singer", "Album", 200000) })));
            using (var session = Session(handler)) Equal(1L, SearchMatch.SearchCandidates(title, "Singer", "Album", 200000, session, trackNumber: "1").Single());
            Equal(1, handler.Calls.Count);
        });
        Test("A literal numbered title in the same results still wins over the stripped title", () =>
        {
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { Song(1, "Lemon", "Singer", "Album", 200000), Song(2, "01 Lemon", "Singer", "Album", 200000) })));
            using (var session = Session(handler)) Equal(2L, SearchMatch.SearchCandidates("01 Lemon", "Singer", "Album", 200000, session, trackNumber: "1").Single());
            Equal(1, handler.Calls.Count);
        });
        foreach (var duration in new[] { 198000L, 202000L })
        Test("Early prefix matching keeps the inclusive two-second length bound: " + duration, () =>
        {
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { Song(1, "Lemon", "Singer", "Album", duration) })));
            using (var session = Session(handler)) Equal(1L, SearchMatch.SearchCandidates("01 Lemon", "Singer", "Album", 200000, session, trackNumber: "1").Single());
            Equal(1, handler.Calls.Count);
        });
        Test("A missing album retains the later prefix fallback rather than accepting early", () =>
        {
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { Song(1, "Lemon", "Singer", "Album", 200000) })));
            using (var session = Session(handler)) Equal(1L, SearchMatch.SearchCandidates("01 Lemon", "Singer", null, 200000, session, trackNumber: "1").Single());
            Equal(4, handler.Calls.Count);
        });
        Test("A spelling-only stripped-title match is not accepted early", () =>
        {
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { Song(1, "Hello Worle", "Singer", "Album", 200000) })));
            using (var session = Session(handler)) Equal(1L, SearchMatch.SearchCandidates("01 Hello World", "Singer", "Album", 200000, session, trackNumber: "1").Single());
            Equal(6, handler.Calls.Count);
        });
        Test("A strong prefix may finish on the first successful later query", () =>
        {
            var handler = new Fixture((_, count) => Json(SearchJson(count <= 2 ? new SearchResultSong[0] : new[] { Song(1, "Lemon", "Singer", "Album", 200000) })));
            using (var session = Session(handler)) Equal(1L, SearchMatch.SearchCandidates("01 Lemon", "Singer", "Album", 200000, session, trackNumber: "1").Single());
            Equal(3, handler.Calls.Count);
        });
        Test("Ambiguous prefix recordings stop before a lyric download or later queries", () =>
        {
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { Song(1, "Lemon", "Singer", "Album", 198000), Song(2, "Lemon", "Singer", "Album", 202000) })));
            using (var session = Session(handler)) Equal(0, SearchMatch.SearchCandidates("01 Lemon", "Singer", "Album", 200000, session, trackNumber: "1").Length);
            Equal(1, handler.Calls.Count);
        });

        foreach (var tail in new[] { "\0", " \t\0\r\n", "\0 \0\u00a0\t", "\u2003\0\r\n" })
        Test("Saved comment accepts only trailing NUL and whitespace padding: " + tail.Length, () =>
        {
            Equal(SavedIdStatus.Valid, SavedNeteaseId.Read(SavedComment() + tail, "Song", 200000, out var id));
            Equal(999L, id);
        });
        Test("Padding cleanup preserves exact prefix length title duration and encoded-data checks", () =>
        {
            var comment = SavedComment();
            Equal(SavedIdStatus.Absent, SavedNeteaseId.Read(" " + comment + "\0", "Song", 200000, out _));
            Equal(SavedIdStatus.Unreadable, SavedNeteaseId.Read(comment.PadRight(SavedNeteaseId.MaximumCommentLength + 1, '\0'), "Song", 200000, out _));
            Equal(SavedIdStatus.Unreadable, SavedNeteaseId.Read(comment.Insert(SavedNeteaseId.Prefix.Length + 12, "\0"), "Song", 200000, out _));
            Equal(SavedIdStatus.Unreadable, SavedNeteaseId.Read(comment + "! \0", "Song", 200000, out _));
            Equal(SavedIdStatus.TitleMismatch, SavedNeteaseId.Read(comment + "\0", "Other Song", 200000, out _));
            Equal(SavedIdStatus.LengthMismatch, SavedNeteaseId.Read(comment + "\0", "Song", 203000, out _));
        });
        Test("A padded saved comment still bypasses searching in the full callback", () =>
        {
            var handler = new Fixture((_, __) => Json(Lyrics));
            Equal("[00:01.00]Hello", Lookup(SavedPlugin(handler, SavedComment() + " \0\r\n"), "Singer", "Song", "Album"));
            Equal(1, handler.Calls.Count); True(handler.Calls.All(c => c.Contains("lyric")));
        });
    }
}
