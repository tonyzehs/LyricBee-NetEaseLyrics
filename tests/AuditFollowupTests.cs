using MusicBeePlugin;
using System;
using System.Linq;

internal static partial class Program
{
    private static void AuditFollowupTests()
    {
        Test("A malformed original cue does not discard either usable auxiliary track", () =>
        {
            foreach (var bad in new[] { "[00:60.00]", "[00:12.1x]", "[999999999999999:01]" })
            foreach (var separate in new[] { true, false })
            {
                var separator = separate ? "\n" : " / ";
                Equal("[00:10.000]one" + separator + "p1" + separator + "t1\ntwo\n[00:14.000]three" + separator + "p3" + separator + "t3",
                    LyricProcessor.MergePronunciation("[00:10]one\n" + bad + "two\n[00:14]three",
                        "[00:10]p1\n[00:12]p2\n[00:14]p3", "[00:10]t1\n[00:12]t2\n[00:14]t3", separate, null, out var tracks));
                Equal(2, tracks);
            }
        });
        Test("Malformed leading trailing and consecutive cues preserve their untimed continuations", () =>
        {
            Equal("[ar:Singer]\nbefore\ncontinued\n[00:10.000]one\n1\nmiddle\ncontinued middle\nmore\n[00:14.000]three\n3\nafter\ncontinued after",
                LyricProcessor.MergePronunciation("[ar:Singer]\n[00:60]before\ncontinued\n[00:10]one\n[00:60]middle\ncontinued middle\n[00:80]more\n[00:14]three\n[00:60]after\ncontinued after",
                    null, "[00:10]1\n[00:14]3", true, null, out var tracks));
            Equal(1, tracks);
        });
        Test("Repeated timestamps do not duplicate a following malformed lyric", () =>
        {
            var text = LyricProcessor.MergePronunciation("[00:10][00:12]one\n[00:60]loose\n[00:14]three", null,
                "[00:10]1\n[00:12]2\n[00:14]3", true, null, out var tracks);
            Equal("[00:10.000]one\n1\nloose\n[00:12.000]one\n2\n[00:14.000]three\n3", text); Equal(1, tracks);
        });
        Test("A mixed valid and invalid repeat-tag line is isolated without dropping other cues", () =>
        {
            Equal("[00:10.000]one\n1\nloose\n[00:14.000]three\n3",
                LyricProcessor.MergePronunciation("[00:10]one\n[00:12][00:60]loose\n[00:14]three", null,
                    "[00:10]1\n[00:12]unused\n[00:14]3", true, null, out var tracks)); Equal(1, tracks);
        });
        Test("Malformed auxiliary cues and their continuations cannot borrow an adjacent valid clock", () =>
        {
            Equal("[00:10.000]one\n1\n[00:12.000]two\n[00:14.000]three\n3",
                LyricProcessor.MergePronunciation("[00:10]one\n[00:12]two\n[00:14]three", null,
                    "[00:10]1\n[00:60]bad\nuntimed continuation\n[00:14]3", true, null, out var tracks)); Equal(1, tracks);
        });
        Test("Missing usable original clocks or auxiliary alignment retains the untouched original", () =>
        {
            foreach (var original in new[] { "[00:60]one\n[00:70]two", "[00:10]one\n[00:60]two\n[00:14]three" })
            {
                Equal(original, LyricProcessor.MergePronunciation(original, null, "[00:40]unpaired", true, null, out var tracks));
                Equal(0, tracks);
            }
        });
        Test("Malformed original text preserves offset metadata and valid raw-time alignment", () =>
        {
            Equal("[offset:500]\n[00:10.000]one\n1\nloose\n[00:14.000]three\n3",
                LyricProcessor.MergePronunciation("[offset:500]\n[00:10]one\n[00:60]loose\n[00:14]three", null,
                    "[00:10]1\n[00:14]3", true, null, out var tracks)); Equal(1, tracks);
        });
        Test("A host lookup with one broken cue keeps pronunciation and translation", () =>
        {
            var lyrics = "{\"code\":200,\"lrc\":{\"lyric\":\"[00:10]にほんご one\\n[00:60]loose\\n[00:14]にほんご three\"}," +
                "\"romalrc\":{\"lyric\":\"[00:10]p1\\n[00:14]p3\"},\"tlyric\":{\"lyric\":\"[00:10]t1\\n[00:14]t3\"}}";
            var handler = PronunciationFixture(lyrics, Wiki("日语"));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
            SetConfig(plugin, ModernSettings(pronunciation: true));
            Equal("[00:10.000]にほんご one\np1\nt1\nloose\n[00:14.000]にほんご three\np3\nt3", Lookup(plugin));
            Equal(1, handler.Calls.Count(c => c.Contains("/lyric")));
        });
        Test("Mixed-case Roman-letter words can use existing long-title spelling allowance", () =>
        {
            foreach (var word in new[] { "Mix", "Liv", "Mc" })
                Equal(1L, Select(new[] { Song(1, word + "x It Up Tonight", "Singer", "Album", 200000) }, word + " It Up Tonight"));
            // Seven-letter titles still require >= .9 similarity; changing numeral handling does not relax that guard.
            Equal(0L, Select(new[] { Song(1, "Mixx It Up", "Singer", "Album", 200000) }, "Mix It Up"));
        });
        Test("Uppercase and numbered-context Romans still protect recording identity", () =>
        {
            Equal(0L, Select(new[] { Song(1, "Rocky III", "Singer", "Album", 200000) }, "Rocky II"));
            Equal(0L, Select(new[] { Song(1, "MIXx It Up Tonight", "Singer", "Album", 200000) }, "MIX It Up Tonight"));
            Equal(0L, Select(new[] { Song(1, "A Beautiful Song No. vi", "Singer", "Album", 200000) }, "A Beautiful Song No. v"));
            Equal(1L, Select(new[] { Song(1, "A Beautiful Song Part 2", "Singer", "Album", 200000) }, "A Beautiful Song Pt. ii"));
            Equal(0L, Select(new[] { Song(1, "A Beautiful Song 12 3", "Singer", "Album", 200000) }, "A Beautiful Song 1 23"));
        });
        Test("Track-tag-confirmed space prefixes work while supplied numbered titles take priority", () =>
        {
            var lemon = Song(1, "Lemon", "Singer", "Album", 200000);
            Equal(1L, SearchMatch.SelectCandidate(new[] { lemon }, "01 Lemon", "Singer", "Album", 200000, trackNumber: "1/12"));
            Equal(0L, SearchMatch.SelectCandidate(new[] { lemon }, "01 Lemon", "Singer", "Album", 200000, trackNumber: "2"));
            var songs = new[] { Song(1, "7 Rings", "Singer", "Album", 200000), Song(2, "Rings", "Singer", "Album", 200000) };
            Equal(1L, SearchMatch.SelectCandidate(songs, "7 Rings", "Singer", "Album", 200000, trackNumber: "7"));
            Equal(1L, SearchMatch.SelectCandidate(songs.Reverse(), "7 Rings", "Singer", "Album", 200000, trackNumber: "7"));
        });
        Test("Plain-space prefix normalization works through the real callback without exceeding search caps", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ?
                SearchJson(new[] { Song(1, "Lemon", "Singer", "Album", 200000) }) : Lyrics));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.TrackTitle ? "01 Lemon" :
                tag == Plugin.MetaDataType.TrackNo ? "1" : null, duration: "3:20");
            Equal("[00:01.00]Hello", Lookup(plugin, "Singer", "ignored", "Album"));
            True(handler.Calls.Count(c => c.Contains("search")) <= LookupSession.MaxSearchRequests);
        });
    }
}
