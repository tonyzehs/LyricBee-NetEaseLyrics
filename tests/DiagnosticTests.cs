using MusicBeePlugin;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static partial class Program
{
    private static void ToggleAdvanced(Panel panel)
    {
        var link = (LinkLabel)panel.Controls["AdvancedDiagnostics"];
        typeof(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(link, new object[] { new LinkLabelLinkClickedEventArgs(link.Links[0]) });
    }

    private static void DiagnosticTests()
    {
        Test("Development labels depend on the declared version and stable builds keep diagnostics", () =>
        {
            foreach (var version in new[] { "1.9.5-test.4", "1.9.5-pre", "1.9.5-pre.1", "1.9.5-pre01" })
                True(Plugin.IsDevelopmentVersion(version));
            foreach (var version in new[] { "1.9.5", "1.9.5-preview", "1.9.5-test.", "1.9.5-test.4-extra", "", null })
                True(!Plugin.IsDevelopmentVersion(version));
            Equal(Plugin.IsDevelopmentVersion(Plugin.BuildVersion), Plugin.BuildDescription.Contains("DEV BUILD"));
            True(typeof(Plugin).Assembly.GetTypes().Any(t => t.Name == "DevelopmentDiagnostics"));
        });
        Test("Bounded reports disclose song ID listening history", () =>
        {
            var recorder = new DevelopmentDiagnostics();
            var stale = recorder.Begin(new NeteaseConfig());
            for (var i = 0; i < 7; i++)
            {
                var lookup = recorder.Begin(new NeteaseConfig());
                for (var j = 0; j < 50; j++) lookup.Add(DiagnosticKind.MatchingSelected, songId: 100 + i);
                lookup.Complete(DiagnosticLookupStatus.Lyrics);
            }
            stale.Add(DiagnosticKind.Custom10Override, songId: 999999); stale.Complete(DiagnosticLookupStatus.Error);
            var report = recorder.Report();
            Equal(5, report.Split('\n').Count(s => s.StartsWith("Lookup ")));
            Equal(200, report.Split('\n').Count(s => s.Contains("ms MatchingSelected ")));
            Equal(5, report.Split('\n').Count(s => s.Contains("dropped_events=10")));
            True(!report.Contains("song_id=999999")); True(report.Contains("listening history"));
            True(Encoding.UTF8.GetByteCount(report) <= DevelopmentDiagnostics.MaximumReportBytes);
            recorder.Clear(); stale.Add(DiagnosticKind.Custom10Override, songId: 999999);
            True(!recorder.HasLookups); True(!recorder.Report().Contains("Lookup "));
        });
        Test("Diagnostic ages are relative to report time and event milliseconds remain intact", () =>
        {
            long now = 0;
            var recorder = new DevelopmentDiagnostics(() => now);
            var first = recorder.Begin(new NeteaseConfig());
            now = 250; first.Add(DiagnosticKind.RequestStarted); first.Complete(DiagnosticLookupStatus.Lyrics);
            now = 119000; recorder.Begin(new NeteaseConfig()).Add(DiagnosticKind.MatchingSelected);
            now = 120000; var report = recorder.Report();
            True(report.Contains("Lookup 1, 2 min before this report")); True(report.Contains("Lookup 2, 1 sec before this report"));
            True(report.Contains("250ms RequestStarted") && report.Contains("elapsed_ms=250"));
            True(!System.Text.RegularExpressions.Regex.IsMatch(report, @"\d{4}-\d{2}-\d{2}T"));
            now = 180000; True(recorder.Report().Contains("Lookup 1, 3 min before this report"));
        });
        Test("Long diagnostics retain the first twenty and last twenty with one exact gap notice", () =>
        {
            foreach (var count in new[] { 0, 1, 20, 21, 39, 40, 41, 50, 200 })
            {
                var recorder = new DevelopmentDiagnostics(); var lookup = recorder.Begin(new NeteaseConfig());
                for (var i = 0; i < count; i++) lookup.Add(DiagnosticKind.RequestStarted, count: i);
                var report = recorder.Report();
                var actual = report.Split('\n').Where(s => s.Contains("ms RequestStarted "))
                    .Select(s => int.Parse(s.Substring(s.LastIndexOf("count=", StringComparison.Ordinal) + 6))).ToArray();
                var expected = Enumerable.Range(0, Math.Min(20, count)).Concat(
                    Enumerable.Range(Math.Max(20, count - 20), Math.Min(20, Math.Max(0, count - 20)))).ToArray();
                True(actual.SequenceEqual(expected));
                Equal(count > 40 ? 1 : 0, report.Split('\n').Count(s => s.Contains("events skipped between")));
                if (count > 40) True(report.Contains("[" + (count - 40) + " events skipped between first 20 and last 20]"));
                True(Encoding.UTF8.GetByteCount(report) <= DevelopmentDiagnostics.MaximumReportBytes);
            }
        });
        Test("Concurrent diagnostic scopes keep their request counts and song IDs separate", () =>
        {
            var recorder = new DevelopmentDiagnostics();
            var lookups = Enumerable.Range(0, 5).Select(_ => recorder.Begin(new NeteaseConfig())).ToArray();
            Parallel.For(0, 5, i =>
            {
                for (var j = 0; j < 30; j++) lookups[i].Add(DiagnosticKind.RequestSuccess, songId: 101 + i, searches: i, lyrics: j);
                lookups[i].Complete(DiagnosticLookupStatus.Lyrics);
            });
            var parts = recorder.Report().Split(new[] { "\nLookup " }, StringSplitOptions.None).Skip(1).ToArray();
            Equal(5, parts.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                True(parts[i].Contains("search_requests=" + i + " lyric_phase_requests=29"));
                Equal(30, parts[i].Split('\n').Count(s => s.Contains("song_id=" + (101 + i) + " ")));
            }
        });
        Test("Diagnostic copy uses saved settings and excludes tags lyrics paths cookies and error strings", () =>
        {
            const string canary = "PRIVATE_CANARY_812";
            var handler = new Fixture((_, __) => Json(PronunciationJson("[00:01]" + canary, roma: null, translation: null)));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : canary);
            SetConfig(plugin, ModernSettings());
            var configPath = Path.Combine(_directories[plugin], "netease_config");
            var saved = File.ReadAllBytes(configPath);
            Equal("[00:01]" + canary, Lookup(plugin));
            string copied = null;
            plugin.DiagnosticClipboardWriter = report => copied = report;
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                ToggleAdvanced(panel);
                ((CheckBox)panel.Controls["ShowTranslation"]).Checked = false;
                var button = (Button)panel.Controls["CopyDiagnosticReport"];
                True(button.Enabled); button.PerformClick();
                True(copied != null && copied.Contains("song_id=101"));
                True(!copied.Contains(canary) && !copied.Contains("requested.flac") && !copied.Contains(_directories[plugin]));
                True(!copied.Contains("http:") && !copied.Contains("https:") && !copied.Contains("MUSIC_U"));
                True(copied.Contains("default=Translation"));
                True(saved.SequenceEqual(File.ReadAllBytes(configPath)));
                plugin.DiagnosticClipboardWriter = _ => { throw new Exception(canary); };
                button.PerformClick(); True(!panel.Controls["DiagnosticPrivacyNote"].Text.Contains(canary));
            }
            Equal("[00:01]" + canary, Lookup(plugin));
            var errors = new Fixture((_, __) => { throw new System.Net.Http.HttpRequestException(canary); });
            var failing = CreatePlugin(errors, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : canary);
            failing.DiagnosticClipboardWriter = report => copied = report;
            Equal(null, Lookup(failing));
            using (var panel = new Panel())
            {
                failing.Configure(panel.Handle); ToggleAdvanced(panel);
                ((Button)panel.Controls["CopyDiagnosticReport"]).PerformClick();
                True(copied.Contains("TransportFailure")); True(!copied.Contains(canary));
            }
        });
        Test("Diagnostic button handles English Chinese empty history and plugin shutdown", () =>
        {
            foreach (var ui in new[] { 1, 2 })
            {
                var handler = new Fixture((_, __) => Json(Lyrics));
                var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
                SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":" + ui));
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    True(!panel.Controls["CopyDiagnosticReport"].Enabled);
                    True(!panel.Controls["CopyDiagnosticReport"].Visible);
                    True(!panel.Controls["DiagnosticPrivacyNote"].Visible);
                    True(!panel.Controls["AdvancedDiagnostics"].Text.Contains("DEV BUILD"));
                    ToggleAdvanced(panel);
                    True(panel.Controls["CopyDiagnosticReport"].Visible);
                    True(panel.Controls["DiagnosticPrivacyNote"].Text.Contains(ui == 1 ? "listening history" : "听歌记录"));
                    ToggleAdvanced(panel);
                    True(!panel.Controls["CopyDiagnosticReport"].Visible);
                    Equal("[00:01.00]Hello", Lookup(plugin));
                    plugin.Configure(panel.Handle);
                    True(!panel.Controls["CopyDiagnosticReport"].Visible);
                    ToggleAdvanced(panel);
                    True(panel.Controls["CopyDiagnosticReport"].Enabled);
                    string copied = null; plugin.DiagnosticClipboardWriter = text => copied = text;
                    ((Button)panel.Controls["CopyDiagnosticReport"]).PerformClick();
                    True(copied != null); Equal(1, handler.Calls.Count);
                    plugin.Close(default(Plugin.PluginCloseReason));
                    plugin.Configure(panel.Handle); True(!panel.Controls["CopyDiagnosticReport"].Enabled);
                }
            }
        });
        Test("Diagnostic refusal reporting adds no transport retries or HTTP downgrade", () =>
        {
            var handler = new Fixture((_, __) => Json("{\"code\":-460,\"message\":\"SECRET_RESPONSE\"}"));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
            Equal(null, Lookup(plugin)); Equal(2, handler.Calls.Count);
            True(handler.Calls.All(c => c.Contains(" https://")));
            string copied = null; plugin.DiagnosticClipboardWriter = report => copied = report;
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle); ToggleAdvanced(panel);
                ((Button)panel.Controls["CopyDiagnosticReport"]).PerformClick();
                True(copied.Contains("code=-460")); True(!copied.Contains("SECRET_RESPONSE"));
                True(copied.Contains("lyric_phase_requests=2"));
            }
        });
    }
}
