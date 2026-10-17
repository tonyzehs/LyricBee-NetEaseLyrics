using MusicBeePlugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static partial class Program
{
    private static void LookupOrderTests()
    {
        Test("All recorded selections and request sequences equal the shipped test.1 baseline", () =>
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NeteaseLyricsTest.Fixtures.LookupOrderBaseline.json"))
            using (var reader = new StreamReader(stream))
                True(Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JToken.Parse(reader.ReadToEnd()), Newtonsoft.Json.Linq.JToken.Parse(LookupReplay())));
        });
        Test("A strong track prefix returns before a later query can supply a literal numbered title", () =>
        {
            var calls = 0;
            var handler = new Fixture((_, __) => Json(SearchJson(new[] {
                calls++ < 2 ? Song(1, "Song", "Singer", "Album", 200000) : Song(2, "01 Song", "Singer", "Album", 200000)
            })));
            using (var session = Session(handler))
                Equal(1L, SearchMatch.SearchCandidates("01 Song", "Singer", "Album", 200000, session, trackNumber: "1").Single());
            Equal(1, handler.Calls.Count);
        });
        Test("Ordinary ambiguity stops before alias requests or later searches", () =>
        {
            var handler = new Fixture((_, __) => Json(SearchJson(new[] {
                Song(1, "Song", "Singer", "Album", 198500), Song(2, "Song", "Singer", "Album", 201500), AliasSong(3)
            })));
            using (var session = Session(handler)) Equal(0, SearchMatch.SearchCandidates("Song", "Singer", "Album", 200000, session).Length);
            Equal(1, handler.Calls.Count); True(!handler.Calls.Any(ArtistCall));
        });
        Test("Track-prefix matching cannot combine with verified artist aliases", () =>
        {
            var handler = AliasFixture(new[] { AliasSong() }, AliasDetails(81, "Localized", "Singer"));
            using (var session = Session(handler))
                Equal(0, SearchMatch.SearchCandidates("01 Song", "Singer", "Album", 200000, session, trackNumber: "1").Length);
            Equal(0, handler.Calls.Count(ArtistCall));
        });
        Test("Ambiguous translated soundtracks cannot fall through to show credits", () =>
        {
            var a = LocalizedSong(); var b = Song(2, "Song (from the film Other Film)", "Singer", "Other Film Soundtrack", 200000);
            a.album.alias = b.album.alias = new[] { "Distant Show Soundtrack" };
            Equal(0L, ShowSelect(new[] { a, b, ShowSong(3) }));
        });
    }
    private static RecordingFixture ReadRecording(string name)
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NeteaseLyricsTest.Fixtures." + name + ".json"))
        using (var reader = new StreamReader(stream)) return JsonConvert.DeserializeObject<RecordingFixture>(reader.ReadToEnd());
    }

    private static string LookupReplay()
    {
        var rows = new List<object>();
        foreach (var f in new[] { ReadRecording("ByYourSide"), ReadSoundtrackFixture() }.Concat(ReadLocalizedRecordings()))
        {
            var handler = new Fixture((r, _) => Json(SearchJson(f.title == "By Your Side" && r.Method == System.Net.Http.HttpMethod.Get ? new[] { f.correct } : f.observed)));
            using (var session = Session(handler))
            {
                var ids = SearchMatch.SearchCandidates(f.title, f.artist, f.album, f.duration_ms, session);
                rows.Add(new { title = f.title, ids, requests = handler.Calls.ToArray() });
            }
        }
        var alias = AliasRecording();
        var aliasHandler = new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("/api/artist/") ? alias["details"].ToString() : alias["search"].ToString()));
        var aliasIds = AliasLookup(aliasHandler, (string)alias["title"], (string)alias["artist"], (string)alias["album"], (long)alias["duration_ms"]);
        rows.Add(new { title = (string)alias["title"], ids = aliasIds, requests = aliasHandler.Calls.ToArray() });
        return JsonConvert.SerializeObject(rows, Formatting.Indented);
    }
}
