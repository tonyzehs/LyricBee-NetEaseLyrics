using MusicBeePlugin;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

internal static partial class Program
{
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
