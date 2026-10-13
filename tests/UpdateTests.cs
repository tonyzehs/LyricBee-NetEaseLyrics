using MusicBeePlugin;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

internal static partial class Program
{
    private static string ReleaseJson(string version = "v9.9.9", bool draft = false, bool prerelease = false) =>
        "{\"tag_name\":\"" + version + "\",\"draft\":" + draft.ToString().ToLowerInvariant() +
        ",\"prerelease\":" + prerelease.ToString().ToLowerInvariant() + ",\"html_url\":\"https://untrusted.invalid/\"}";

    private sealed class UpdateFixture : HttpMessageHandler
    {
        internal int Calls;
        internal string Uri, Method, Headers;
        internal CancellationToken Token;
        internal Func<CancellationToken, Task<HttpResponseMessage>> Respond;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; Uri = request.RequestUri.AbsoluteUri; Method = request.Method.Method;
            Headers = request.Headers.ToString(); Token = token;
            return Respond(token);
        }
    }

    private static UpdateFixture UpdateResponse(string json) => new UpdateFixture { Respond = _ => Task.FromResult(Json(json)) };
    private static void PumpUpdate(Func<bool> finished)
    {
        var guard = Stopwatch.StartNew();
        while (!finished())
        {
            if (guard.Elapsed > TimeSpan.FromSeconds(10)) throw new Exception("Update fixture did not complete");
            Application.DoEvents();
        }
    }

    private static void UpdateTests()
    {
        foreach (var pair in new[] {
            new[] { "1.9.9", "v1.9.10", "True" }, new[] { "1.9.10", "v1.9.9", "False" },
            new[] { "1.9.5", "v1.9.5", "False" }, new[] { "1.9.6-test.1", "v1.9.5", "False" },
            new[] { "1.9.6-test.1", "v1.9.6", "True" }, new[] { "1.9.6-pre", "v1.9.6", "True" },
            new[] { "2.0.0", "v1.99.99", "False" }, new[] { "1.99.99", "v2.0.0", "True" } })
        {
            Test("Stable update comparison " + pair[0] + " -> " + pair[1], () =>
            {
                var handler = UpdateResponse(ReleaseJson(pair[1]));
                var result = new UpdateChecker(() => handler).CheckAsync(pair[0], CancellationToken.None).GetAwaiter().GetResult();
                True(result.Succeeded); Equal(bool.Parse(pair[2]), result.UpdateAvailable); Equal(1, handler.Calls);
                Equal("https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/" + pair[1], result.ReleasePage);
            });
        }
        Test("Update checks send one fixed anonymous HTTPS GET without song metadata", () =>
        {
            var handler = UpdateResponse(ReleaseJson());
            var result = new UpdateChecker(() => handler).CheckAsync("1.9.5", CancellationToken.None).GetAwaiter().GetResult();
            True(result.Succeeded); Equal(UpdateChecker.LatestReleaseApi, handler.Uri); Equal("GET", handler.Method);
            True(handler.Headers.Contains("LyricBee-NetEaseLyrics"));
            True(!handler.Headers.Contains("Authorization") && !handler.Headers.Contains("Cookie"));
            var field = typeof(UpdateChecker).GetField("_handlerFactory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            using (var production = ((Func<HttpMessageHandler>)field.GetValue(new UpdateChecker()))())
            {
                True(!((HttpClientHandler)production).AllowAutoRedirect); True(!((HttpClientHandler)production).UseCookies);
            }
        });
        foreach (var json in new[] { "null", "[]", "<html>blocked</html>", "{}", "{\"tag_name\":\"v9.9.9\"}",
            ReleaseJson(draft: true), ReleaseJson(prerelease: true), ReleaseJson("v1.9.6-test.1"),
            ReleaseJson("v99999999999999.0.0"), ReleaseJson("v9.9.9/../../elsewhere") })
        {
            Test("Malformed or unpublished release cannot offer an update: " + json, () =>
            {
                var handler = UpdateResponse(json);
                var result = new UpdateChecker(() => handler).CheckAsync("1.9.5", CancellationToken.None).GetAwaiter().GetResult();
                True(!result.Succeeded && !result.UpdateAvailable && result.ReleasePage == null); Equal(1, handler.Calls);
            });
        }
        Test("Refused oversized cancelled and failed update requests return an inline failure result", () =>
        {
            foreach (var code in new[] { HttpStatusCode.Forbidden, (HttpStatusCode)429, HttpStatusCode.BadGateway, HttpStatusCode.Redirect })
            {
                var handler = new UpdateFixture { Respond = _ => Task.FromResult(new HttpResponseMessage(code)) };
                True(!new UpdateChecker(() => handler).CheckAsync("1.9.5", CancellationToken.None).GetAwaiter().GetResult().Succeeded);
                Equal(1, handler.Calls);
            }
            var oversized = UpdateResponse(ReleaseJson() + new string(' ', UpdateChecker.MaximumResponseBytes + 1));
            True(!new UpdateChecker(() => oversized).CheckAsync("1.9.5", CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            var failed = new UpdateFixture { Respond = _ => throw new HttpRequestException("private error") };
            True(!new UpdateChecker(() => failed).CheckAsync("1.9.5", CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            using (var cancellation = new CancellationTokenSource())
            {
                var pending = new TaskCompletionSource<HttpResponseMessage>();
                var handler = new UpdateFixture { Respond = token => { token.Register(() => pending.TrySetCanceled()); return pending.Task; } };
                var check = new UpdateChecker(() => handler).CheckAsync("1.9.5", cancellation.Token);
                cancellation.Cancel(); True(!check.GetAwaiter().GetResult().Succeeded); Equal(1, handler.Calls);
            }
        });
        Test("Unknown installed versions do not request or invent an available update", () =>
        {
            var handler = UpdateResponse(ReleaseJson());
            True(!new UpdateChecker(() => handler).CheckAsync("broken", CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            Equal(0, handler.Calls);
        });
        Test("Bilingual LyricBee update results only open a browser after View update is clicked", () =>
        {
            foreach (var ui in new[] { 1, 2 })
            {
                var handler = UpdateResponse(ReleaseJson());
                var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
                SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":" + ui));
                plugin.UpdateChecker = new UpdateChecker(() => handler);
                string opened = null; plugin.ReleasePageOpener = url => opened = url;
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    Equal(0, handler.Calls); True(!panel.Controls["UpdateFound"].Visible);
                    True(panel.Controls["UpdateStatus"].Text.Contains(Plugin.BuildVersion));
                    var check = (Button)panel.Controls["CheckForUpdates"];
                    True(check.Text == (ui == 1 ? "Check for LyricBee updates" : "检查 LyricBee 更新"));
                    Equal(ui == 1 ? "View update" : "查看更新", panel.Controls["UpdateFound"].Text);
                    var windows = Application.OpenForms.Count;
                    check.PerformClick(); PumpUpdate(() => check.Enabled);
                    Equal(windows, Application.OpenForms.Count);
                    Equal(1, handler.Calls); Equal(null, opened); True(panel.Controls["UpdateFound"].Visible);
                    True(panel.Controls["UpdateStatus"].Text.Contains("9.9.9"));
                    ((Button)panel.Controls["UpdateFound"]).PerformClick();
                    Equal("https://github.com/tonyzehs/LyricBee-NetEaseLyrics/releases/tag/v9.9.9", opened);
                    plugin.ReleasePageOpener = _ => throw new Exception("private error");
                    ((Button)panel.Controls["UpdateFound"]).PerformClick();
                    True(!panel.Controls["UpdateStatus"].Text.Contains("private error"));
                    True(panel.Controls["UpdateStatus"].Text.Contains(ui == 1 ? "Could not open" : "无法打开"));
                }
            }
        });
        Test("Pending update checks cancel on settings rebuild or close and cannot alter saved preferences", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":1"));
            var path = System.IO.Path.Combine(_directories[plugin], "netease_config");
            var saved = System.IO.File.ReadAllText(path);
            var pending = new TaskCompletionSource<HttpResponseMessage>();
            var handler = new UpdateFixture { Respond = token => { token.Register(() => pending.TrySetCanceled()); return pending.Task; } };
            plugin.UpdateChecker = new UpdateChecker(() => handler);
            var opened = false; plugin.ReleasePageOpener = _ => opened = true;
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                var check = (Button)panel.Controls["CheckForUpdates"];
                check.PerformClick(); True(!check.Enabled); check.PerformClick(); Equal(1, handler.Calls);
                True(panel.Controls["UpdateStatus"].Text.Contains("Checking"));
                ((CheckBox)panel.Controls["ShowTranslation"]).Checked = false;
                ((ComboBox)panel.Controls["InterfaceLanguage"]).SelectedIndex = 2;
                True(handler.Token.IsCancellationRequested); True(check.IsDisposed);
                Application.DoEvents(); True(panel.Controls["CheckForUpdates"].Enabled);
                True(!panel.Controls["UpdateFound"].Visible); True(!opened);
                Equal(saved, System.IO.File.ReadAllText(path));
            }
            var closePending = new TaskCompletionSource<HttpResponseMessage>();
            var closeHandler = new UpdateFixture { Respond = token => { token.Register(() => closePending.TrySetCanceled()); return closePending.Task; } };
            plugin.UpdateChecker = new UpdateChecker(() => closeHandler);
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle); ((Button)panel.Controls["CheckForUpdates"]).PerformClick();
            }
            True(closeHandler.Token.IsCancellationRequested); Application.DoEvents();
            Equal(saved, System.IO.File.ReadAllText(path)); True(!opened);
        });
        Test("Failed and up-to-date UI checks keep View update hidden", () =>
        {
            foreach (var response in new[] { "not JSON", ReleaseJson("v1.9.5") })
            {
                var handler = UpdateResponse(response);
                var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
                SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":1"));
                plugin.UpdateChecker = new UpdateChecker(() => handler);
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle); var check = (Button)panel.Controls["CheckForUpdates"];
                    check.PerformClick(); PumpUpdate(() => check.Enabled);
                    True(!panel.Controls["UpdateFound"].Visible);
                    True(panel.Controls["UpdateStatus"].Text.Contains(response == "not JSON" ? "Could not check" : "No newer stable release"));
                }
            }
        });
        Test("Version labels and update parsing use the same recognized grammar", () =>
        {
            foreach (var value in new[] { "1.9.6", "1.9.6-test.3", "1.9.6-pre", "1.9.6-pre.1", "1.9.6-pre01",
                "1.9.6-preview", "1.9.6-test.", "1.9.6-test.3-extra", "99999999999999.0.0-test.1", "", null })
            {
                var valid = UpdateChecker.TryVersion(value, false, out _, out var development);
                Equal(valid && development, Plugin.IsDevelopmentVersion(value));
            }
            True(UpdateChecker.TryVersion("v1.9.6", true, out _, out _));
            True(!Plugin.IsDevelopmentVersion("v1.9.6-test.3"));
        });
        Test("The response budget accepts a 200 KiB valid reply and rejects more than 256 KiB", () =>
        {
            foreach (var bytes in new[] { 200 * 1024, UpdateChecker.MaximumResponseBytes, UpdateChecker.MaximumResponseBytes + 1 })
            {
                var reply = ReleaseJson();
                var handler = UpdateResponse(reply + new string(' ', bytes - reply.Length));
                var result = new UpdateChecker(() => handler).CheckAsync("1.9.5", CancellationToken.None).GetAwaiter().GetResult();
                Equal(bytes <= UpdateChecker.MaximumResponseBytes, result.Succeeded); Equal(1, handler.Calls);
            }
        });
        Test("Each failed update emits one categorized trace without private exception or response text", () =>
        {
            var cases = new[] {
                Tuple.Create<UpdateFixture,string>(UpdateResponse("PRIVATE_REPLY"), "unreadable reply"),
                Tuple.Create<UpdateFixture,string>(new UpdateFixture { Respond = _ => throw new HttpRequestException("PRIVATE_EXCEPTION") }, "connection failure"),
                Tuple.Create<UpdateFixture,string>(new UpdateFixture { Respond = _ => throw new TaskCanceledException("PRIVATE_EXCEPTION") }, "timeout"),
                Tuple.Create<UpdateFixture,string>(new UpdateFixture { Respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)) }, "HTTP 502") };
            foreach (var item in cases)
            {
                var trace = new List<string>();
                True(!new UpdateChecker(() => item.Item1, trace.Add).CheckAsync("1.9.5", CancellationToken.None).GetAwaiter().GetResult().Succeeded);
                Equal("Update check failed: " + item.Item2, trace.Single());
            }
            var brokenLogger = new UpdateChecker(() => UpdateResponse("PRIVATE_REPLY"), _ => throw new Exception("PRIVATE_LOGGER"));
            True(!brokenLogger.CheckAsync("1.9.5", CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            var successTrace = new List<string>();
            True(new UpdateChecker(() => UpdateResponse(ReleaseJson()), successTrace.Add).CheckAsync("1.9.5", CancellationToken.None).GetAwaiter().GetResult().Succeeded);
            Equal(0, successTrace.Count);
        });
        Test("Rate limiting is distinguished in both languages only for 429 or exhausted 403", () =>
        {
            foreach (var ui in new[] { 1, 2 })
            foreach (var item in new[] { Tuple.Create(429, "1", true), Tuple.Create(403, "0", true), Tuple.Create(403, "1", false), Tuple.Create(502, "0", false) })
            {
                var handler = new UpdateFixture { Respond = _ =>
                {
                    var response = new HttpResponseMessage((HttpStatusCode)item.Item1);
                    response.Headers.Add("X-RateLimit-Remaining", item.Item2); return Task.FromResult(response);
                } };
                var traces = new List<string>();
                var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
                SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":" + ui));
                plugin.UpdateChecker = new UpdateChecker(() => handler, traces.Add);
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    Equal((ui == 1 ? "Installed: LyricBee " : "已安装：LyricBee ") + Plugin.BuildVersion, panel.Controls["UpdateStatus"].Text);
                    var check = (Button)panel.Controls["CheckForUpdates"]; check.PerformClick(); PumpUpdate(() => check.Enabled);
                    Equal(SettingsText.Get(item.Item3 ? SettingsLabel.UpdateRateLimited : SettingsLabel.UpdateFailed, (NeteaseConfig.UiLanguage)ui), panel.Controls["UpdateStatus"].Text);
                    True(!panel.Controls["UpdateFound"].Visible); Equal(1, handler.Calls); Equal(1, traces.Count);
                }
            }
        });
        Test("Unexpected update-button errors remain inline and reach the safe MusicBee trace", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":1"));
            var field = typeof(Plugin).GetField("_mbApiInterface", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var api = (Plugin.MusicBeeApiInterface)field.GetValue(plugin); var trace = new List<string>();
            api.MB_Trace = message => trace.Add(message); field.SetValue(plugin, api);
            plugin.UpdateChecker = null;
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle); var check = (Button)panel.Controls["CheckForUpdates"];
                var windows = Application.OpenForms.Count; check.PerformClick(); PumpUpdate(() => check.Enabled);
                Equal(windows, Application.OpenForms.Count); True(!panel.Controls["UpdateFound"].Visible);
                Equal(SettingsText.Get(SettingsLabel.UpdateFailed, NeteaseConfig.UiLanguage.English), panel.Controls["UpdateStatus"].Text);
                True(trace.Single().EndsWith("Update check failed: internal failure"));
            }
        });
    }
}
