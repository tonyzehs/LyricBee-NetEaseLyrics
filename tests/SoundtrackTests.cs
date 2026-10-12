using MusicBeePlugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static partial class Program
{
    private const string SourceAlbum = "Arcane League of Legends: Season 2 (Soundtrack from the Animated Series)";
    private const string SourceTitle = "Ma Meilleure Ennemie";
    private const string Attribution = " (from the series Arcane League of Legends)";
    private static RecordingFixture ReadSoundtrackFixture()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NeteaseLyricsTest.Fixtures.MaMeilleureEnnemie.json"))
        using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8))
            return JsonConvert.DeserializeObject<RecordingFixture>(reader.ReadToEnd());
    }
    private static void SoundtrackTests()
    {
        Test("Captured Ma Meilleure Ennemie results select the supplied recording without retagging", () =>
        {
            var recorded = ReadSoundtrackFixture();
            foreach (var songs in new[] { recorded.observed, recorded.observed.Reverse().ToArray() })
                Equal(recorded.correct.id, Select(songs, recorded.title, recorded.artist, recorded.album, recorded.duration_ms));
        });
        Test("Soundtrack attribution applies in both directions and both spelling choices", () =>
        {
            var recorded = ReadSoundtrackFixture();
            foreach (var typos in new[] { false, true })
            {
                Equal(recorded.correct.id, SearchMatch.SelectCandidate(recorded.observed, recorded.title,
                    recorded.artist, recorded.album, recorded.duration_ms, typos));
                var bare = Song(1, SourceTitle, "Singer", SourceAlbum, 200000);
                Equal(1L, SearchMatch.SelectCandidate(new[] { bare }, SourceTitle + Attribution, "Singer", SourceAlbum, 200000, typos));
            }
        });
        Test("Recognized soundtrack attribution handles brackets dash quotation and full-width text", () =>
        {
            foreach (var suffix in new[] { Attribution, " [From the film Arcane League of Legends]",
                " - From the video game Arcane League of Legends", " (From \"Arcane League of Legends\")",
                " (from Arcane)", "（From the series Arcane League of Legends）", " (From the original soundtrack of Arcane League of Legends)" })
                Equal(1L, Select(new[] { Song(1, SourceTitle + suffix, "Singer", SourceAlbum, 200000) }, SourceTitle, album: SourceAlbum));
        });
        Test("Attribution removal requires the named work in a nonempty album", () =>
        {
            foreach (var album in new[] { null, "", "Other Album" })
                Equal(0L, Select(new[] { Song(1, SourceTitle + Attribution, "Singer", SourceAlbum, 200000) }, SourceTitle, album: album));
            Equal(1L, Select(new[] { Song(1, SourceTitle + Attribution, "Singer", SourceAlbum, 200000) }, SourceTitle,
                album: "Arcane League of Legends Extended Soundtrack"));
        });
        Test("Attribution removal requires all supplied artist credits exactly", () =>
        {
            var credited = Song(1, SourceTitle + Attribution, "Singer", SourceAlbum, 200000);
            credited.artists = new[] { new SearchResultArtist { name = "Singer" }, new SearchResultArtist { name = "Guest" } };
            Equal(1L, Select(new[] { credited }, SourceTitle, "Guest;Singer", SourceAlbum));
            foreach (var artists in new[] { "Singer", "Singer;Other", null, "Singeer;Guest" })
                Equal(0L, Select(new[] { credited }, SourceTitle, artists, SourceAlbum));
        });
        Test("Attribution duration guard includes two seconds and rejects missing or longer differences", () =>
        {
            var song = Song(1, SourceTitle + Attribution, "Singer", SourceAlbum, 202000);
            Equal(1L, Select(new[] { song }, SourceTitle, album: SourceAlbum));
            song.duration = 202001;
            Equal(0L, Select(new[] { song }, SourceTitle, album: SourceAlbum));
            song.duration = 0;
            Equal(0L, Select(new[] { song }, SourceTitle, album: SourceAlbum));
            song.duration = 200000;
            Equal(0L, Select(new[] { song }, SourceTitle, album: SourceAlbum, duration: null));
        });
        Test("The attributed work must occur in the shared album name", () =>
        {
            Equal(0L, Select(new[] { Song(1, SourceTitle + " (from the series Another World)", "Singer", SourceAlbum, 200000) }, SourceTitle, album: SourceAlbum));
            Equal(0L, Select(new[] { Song(1, SourceTitle + Attribution, "Singer", "Other Soundtrack", 200000) }, SourceTitle, album: "Other Soundtrack"));
        });
        Test("Bare from-text requires soundtrack context and cannot change substantive titles", () =>
        {
            Equal(0L, Select(new[] { Song(1, "Song (from Tomorrow)", "Singer", "Tomorrow", 200000) }, album: "Tomorrow"));
            Equal(0L, Select(new[] { Song(1, "Song From the Series Arcane", "Singer", SourceAlbum, 200000) }, album: SourceAlbum));
            Equal(0L, Select(new[] { Song(1, "Song (Arcane soundtrack)", "Singer", SourceAlbum, 200000) }, album: SourceAlbum));
            Equal(0L, Select(new[] { Song(1, "Song (from A)", "Singer", "A Soundtrack", 200000) }, album: "A Soundtrack"));
        });
        Test("Only a terminal attribution is removable", () =>
        {
            Equal(0L, Select(new[] { Song(1, "(from the series Arcane) Song", "Singer", SourceAlbum, 200000) }, album: SourceAlbum));
            Equal(0L, Select(new[] { Song(1, "Song (from the series Arcane) Another Song", "Singer", SourceAlbum, 200000) }, album: SourceAlbum));
        });
        Test("Soundtrack notes cannot hide live remix cover instrumental or edit variants", () =>
        {
            foreach (var version in new[] { "Live", "Remix", "Cover", "Instrumental", "Radio Edit", "Original Mix", "Piano Version", "Sped Up" })
            {
                Equal(0L, Select(new[] { Song(1, "Song (" + version + ")" + Attribution, "Singer", SourceAlbum, 200000) }, album: SourceAlbum));
                Equal(0L, Select(new[] { Song(1, "Song" + Attribution + " (" + version + ")", "Singer", SourceAlbum, 200000) }, album: SourceAlbum));
                Equal(0L, Select(new[] { Song(1, "Song (from the series Arcane League of Legends - " + version + ")", "Singer", SourceAlbum, 200000) }, album: SourceAlbum));
            }
        });
        Test("Version words within the attributed work do not imply a different recording", () =>
            Equal(1L, Select(new[] { Song(1, "Song (from the film Live Forever)", "Singer", "Live Forever Soundtrack", 200000) }, album: "Live Forever Soundtrack")));
        Test("Attribution removal cannot erase title numbers or permit a base-title typo", () =>
        {
            Equal(0L, Select(new[] { Song(1, "Theme Part 2" + Attribution, "Singer", SourceAlbum, 200000) }, "Theme Part 1", album: SourceAlbum));
            Equal(0L, Select(new[] { Song(1, "A Beautiful Sogn" + Attribution, "Singer", SourceAlbum, 200000) }, "A Beautiful Song", album: SourceAlbum));
            Equal(1L, Select(new[] { Song(1, "Theme Part 2" + Attribution, "Singer", SourceAlbum, 200000) }, "Theme Pt. II", album: SourceAlbum));
        });
        Test("Conflicting work attributions cannot be accepted as spelling differences", () =>
        {
            const string album = "Arcane Dawn Arcane Down Soundtrack";
            foreach (var duration in new long?[] { 200000, null })
                Equal(0L, Select(new[] { Song(1, "Song (from the series Arcane Down)", "Singer", album, 200000) },
                    "Song (from the series Arcane Dawn)", album: album, duration: duration));
        });
        Test("Conflicting work attributions cannot merge through an unannotated duplicate", () =>
        {
            const string album = "Arcane One Arcane Two Soundtrack";
            var songs = new[] { Song(1, "Song (from the series Arcane One)", "Singer", album, 200000),
                Song(2, "Song", "Singer", album, 200000), Song(3, "Song (from the series Arcane Two)", "Singer", album, 200000) };
            Equal(0L, Select(songs, album: album));
            Equal(0L, Select(songs.Reverse(), album: album));
        });
        Test("Matching annotated and plain listings remain compatible duplicates", () =>
        {
            var songs = new[] { Song(1, "Song" + Attribution, "Singer", SourceAlbum, 200000), Song(2, "Song", "Singer", SourceAlbum, 201000) };
            Equal(1L, Select(songs, album: SourceAlbum));
            Equal(1L, Select(songs.Reverse(), album: SourceAlbum));
        });
        Test("Captured soundtrack replay fetches correct lyrics through both host matching modes", () =>
        {
            var recorded = ReadSoundtrackFixture();
            foreach (var legacy in new[] { false, true })
            {
                var handler = new Fixture((request, _) =>
                {
                    if (request.RequestUri.AbsolutePath.Contains("search")) return Json(SearchJson(recorded.observed));
                    True(request.Method == System.Net.Http.HttpMethod.Post || request.RequestUri.Query.Contains("id=" + recorded.correct.id));
                    return Json(Lyrics);
                });
                var plugin = CreatePlugin(handler);
                SetConfig(plugin, "{\"Format\":0,\"UseLegacyMatch\":" + (legacy ? "true" : "false") + ",\"Fuzzy\":false}");
                var field = typeof(Plugin).GetField("_mbApiInterface", BindingFlags.Instance | BindingFlags.NonPublic);
                var api = (Plugin.MusicBeeApiInterface)field.GetValue(plugin);
                api.Library_GetFileProperty = (_, property) => property == Plugin.FilePropertyType.Duration ? "2:27.973" : null;
                field.SetValue(plugin, api);
                Equal("[00:01.00]Hello", Lookup(plugin, recorded.artist, recorded.title, recorded.album));
                Equal(1, handler.Calls.Count(c => c.Contains("search")));
                Equal(1, handler.Calls.Count(c => c.Contains("lyric")));
            }
        });
        Test("Soundtrack attribution traces disclose the rule without song titles or album tags", () =>
        {
            var trace = new List<string>();
            var handler = new Fixture((_, __) => Json(SearchJson(new[] { Song(1, SourceTitle + Attribution, "Singer", SourceAlbum, 200000) })));
            using (var session = Session(handler, trace.Add))
                Equal(1L, SearchMatch.SearchAndMatch(SourceTitle, "Singer", SourceAlbum, 200000, session));
            True(trace.Any(t => t.Contains("soundtrack attribution")));
            True(trace.All(t => !t.Contains(SourceTitle) && !t.Contains(SourceAlbum)));
            Equal(1, handler.Calls.Count);
        });
    }
}
