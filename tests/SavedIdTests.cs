using MusicBeePlugin;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static partial class Program
{
    private static string SavedComment(string plain)
    {
        // Independent fixture encoder, checked against the real download format.
        using (var aes = Aes.Create())
        {
            aes.Key = new byte[] { 0x23,0x31,0x34,0x6c,0x6a,0x6b,0x5f,0x21,0x5c,0x5d,0x26,0x30,0x55,0x3c,0x27,0x28 };
            aes.Mode = CipherMode.ECB; aes.Padding = PaddingMode.PKCS7;
            var bytes = Encoding.UTF8.GetBytes(plain);
            using (var encrypt = aes.CreateEncryptor())
                return SavedNeteaseId.Prefix + Convert.ToBase64String(encrypt.TransformFinalBlock(bytes, 0, bytes.Length));
        }
    }
    private static string SavedComment(long id = 999, string title = "Song", long duration = 200000) =>
        SavedComment("music:" + JsonConvert.SerializeObject(new { musicId = id, musicName = title, duration, artist = new[] { new object[] { "Old Artist", 51 } }, album = "Old Album" }));
    private static Plugin SavedPlugin(Fixture fixture, string comment, string duration = "3:20", string custom = null) =>
        CreatePlugin(fixture, (_, tag) => tag == Plugin.MetaDataType.Comment ? comment : tag == Plugin.MetaDataType.Custom10 ? custom : null, duration);

    private static void SavedIdTests()
    {
        Test("Saved NetEase ID fetches lyrics without searching despite changed credits", () =>
        {
            var handler = new Fixture((r, _) => { True(r.RequestUri.AbsolutePath.Contains("lyric")); return Json(Lyrics); });
            Equal("[00:01.00]Hello", Lookup(SavedPlugin(handler, SavedComment()), "New Artist", "Song", "New Album"));
            Equal(1, handler.Calls.Count);
        });
        Test("Saved ID title normalization and inclusive two-second length bound", () =>
        {
            foreach (var length in new[] { 198000, 202000 })
            {
                Equal(SavedIdStatus.Valid, SavedNeteaseId.Read(SavedComment(title: "Ｓｏｎｇ!", duration: length), "Song", 200000, out var id));
                Equal(999L, id);
            }
        });
        foreach (var pair in new[] {
            Tuple.Create((string)null, SavedIdStatus.Absent), Tuple.Create("Other user comment", SavedIdStatus.Absent),
            Tuple.Create("wrong prefix:" + SavedComment(), SavedIdStatus.Absent),
            Tuple.Create(SavedNeteaseId.Prefix + "invalid base64!", SavedIdStatus.Unreadable),
            Tuple.Create(SavedNeteaseId.Prefix + new string('a', 8200), SavedIdStatus.Unreadable),
            Tuple.Create(SavedComment("music:not json"), SavedIdStatus.Unreadable),
            Tuple.Create(SavedComment("dj:{\"musicId\":999}"), SavedIdStatus.Unreadable),
            Tuple.Create(SavedComment("music:{\"musicId\":0}"), SavedIdStatus.Unreadable),
            Tuple.Create(SavedComment("music:{\"musicId\":999,\"musicName\":\"Song\"}"), SavedIdStatus.LengthMismatch),
            Tuple.Create(SavedComment("music:{\"musicId\":999,\"musicName\":null,\"duration\":200000}"), SavedIdStatus.TitleMismatch),
            Tuple.Create(SavedComment(title: "Other Song"), SavedIdStatus.TitleMismatch),
            Tuple.Create(SavedComment(duration: 197999), SavedIdStatus.LengthMismatch),
            Tuple.Create(SavedComment(duration: 202001), SavedIdStatus.LengthMismatch),
            Tuple.Create(SavedComment(duration: 0), SavedIdStatus.LengthMismatch) })
        Test("Invalid or absent saved ID searches normally: " + pair.Item2 + " " + (pair.Item1?.Length ?? 0), () =>
        {
            Equal(pair.Item2, SavedNeteaseId.Read(pair.Item1, "Song", 200000, out var id)); Equal(0L, id);
            var handler = new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics));
            Equal("[00:01.00]Hello", Lookup(SavedPlugin(handler, pair.Item1), "Singer", "Song", "Album"));
            Equal(2, handler.Calls.Count);
        });
        foreach (var duration in new[] { (string)null, "", "broken", "0:00", "3:17" })
        Test("Saved ID ignores unavailable or conflicting host duration " + duration, () =>
        {
            var handler = new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics));
            Equal("[00:01.00]Hello", Lookup(SavedPlugin(handler, SavedComment(), duration), "Singer", "Song", "Album"));
            Equal(duration == "3:17" ? 4 : 2, handler.Calls.Count);
        });
        foreach (var reply in new[] { "{\"code\":200}", "{\"code\":200,\"nolyric\":true}", "{\"code\":-460}" })
        Test("Unavailable saved ID falls through to ordinary matching: " + reply, () =>
        {
            var searching = false;
            var handler = new Fixture((r, _) => {
                if (r.RequestUri.AbsolutePath.Contains("search")) { searching = true; return Json(Search); }
                return Json(searching ? Lyrics : reply);
            });
            Equal("[00:01.00]Hello", Lookup(SavedPlugin(handler, SavedComment()), "Singer", "Song", "Album"));
            Equal(1, handler.Calls.Count(c => c.Contains("search")));
        });
        Test("Failed saved ID reuses its result if search finds the same ID and tries a duplicate", () =>
        {
            var searching = false; var lyricCalls = 0;
            var songs = new[] { Song(999, "Song", "Singer", "Album", 200000), Song(1000, "Song", "Singer", "Album", 200000) };
            var handler = new Fixture((r, _) => {
                if (r.RequestUri.AbsolutePath.Contains("search")) { searching = true; return Json(SearchJson(songs)); }
                lyricCalls++; return Json(searching ? Lyrics : "{\"code\":200,\"nolyric\":true}");
            });
            Equal("[00:01.00]Hello", Lookup(SavedPlugin(handler, SavedComment()), "Singer", "Song", "Album"));
            Equal(2, lyricCalls); Equal(3, handler.Calls.Count);
        });
        Test("Custom10 remains final and never reads the saved comment", () =>
        {
            var handler = new Fixture((r, _) => { True(r.RequestUri.AbsolutePath.Contains("lyric")); return Json("{\"code\":200,\"nolyric\":true}"); });
            var plugin = CreatePlugin(handler, (_, tag) => {
                if (tag == Plugin.MetaDataType.Comment) throw new InvalidOperationException("Must not read comment");
                return tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null;
            });
            Equal(null, Lookup(plugin, "Singer", "Song", "Album")); Equal(1, handler.Calls.Count);
        });
        Test("Saved-ID diagnostics contain only the song ID or a reason category", () =>
        {
            foreach (var valid in new[] { true, false })
            {
                var handler = new Fixture((r, _) => Json(r.RequestUri.AbsolutePath.Contains("search") ? Search : Lyrics));
                var comment = SavedComment(title: valid ? "Song" : "Private Title");
                var plugin = SavedPlugin(handler, comment); Lookup(plugin, "Singer", "Song", "Album");
                var diagnostics = (DevelopmentDiagnostics)typeof(Plugin).GetField("_diagnostics", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(plugin);
                var report = diagnostics.Report();
                True(report.Contains(valid ? "SavedIdUsed" : "SavedIdIgnored"));
                foreach (var secret in new[] { comment, "music:", "Private Title", "Old Album", "Old Artist", SavedNeteaseId.Prefix }) True(!report.Contains(secret));
            }
        });
    }
}
