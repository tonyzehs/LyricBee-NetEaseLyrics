using MusicBeePlugin;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static partial class Program
{
    private static ComboBox LanguageControl(Panel panel, string language) =>
        panel.Controls.OfType<ComboBox>().Single(c => c.Name == language + "Content");

    private static void ContentChoiceTests()
    {
        Test("Both languages expose follow-default and four original-inclusive choices", () =>
        {
            var plugin = PronunciationPlugin(new Fixture((_, __) => Json(Lyrics)), "\"JapaneseText\":0,\"KoreanText\":0");
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                foreach (var language in new[] { "Japanese", "Korean" })
                {
                    var control = LanguageControl(panel, language);
                    Equal("Follow default|Lyrics|Lyrics + Translation|Lyrics + Pronunciation|Lyrics + Pronunciation + Translation",
                        string.Join("|", control.Items.Cast<string>()));
                    Equal(2, control.SelectedIndex);
                }
            }
        });
        Test("All four UI choices persist their stable values and reopen independently", () =>
        {
            var storedValues = new[] { 3, 0, 1, 2 };
            var plugin = PronunciationPlugin(new Fixture((_, __) => Json(Lyrics)), "\"JapaneseText\":0");
            for (var i = 0; i < storedValues.Length; i++)
            {
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    LanguageControl(panel, "Japanese").SelectedIndex = i + 1;
                    LanguageControl(panel, "Korean").SelectedIndex = 4 - i;
                    plugin.SaveSettings();
                }
                var saved = JObject.Parse(File.ReadAllText(Path.Combine(_directories[plugin], "netease_config")));
                Equal(storedValues[i], (int)saved["JapaneseText"]);
                Equal(storedValues[3 - i], (int)saved["KoreanText"]);
                Equal(1, (int)saved["Format"]); Equal(true, (bool)saved["TwoLineTranslation"]);
                plugin.ReadConfig();
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    Equal(i + 1, LanguageControl(panel, "Japanese").SelectedIndex);
                    Equal(4 - i, LanguageControl(panel, "Korean").SelectedIndex);
                }
            }
        });
        Test("Unset language controls fall back to translation without damaging HTTP opt-out", () =>
        {
            var plugin = PronunciationPlugin(new Fixture((_, __) => Json(Lyrics)), "\"AllowInsecureHttp\":false");
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                LanguageControl(panel, "Japanese").SelectedIndex = -1;
                LanguageControl(panel, "Korean").SelectedIndex = -1;
                plugin.SaveSettings();
            }
            var saved = JObject.Parse(File.ReadAllText(Path.Combine(_directories[plugin], "netease_config")));
            Equal(0, (int)saved["JapaneseText"]); Equal(0, (int)saved["KoreanText"]);
            Equal(false, (bool)saved["AllowInsecureHttp"]);
        });
        Test("All four content choices render independently for both languages in both combined layouts", () =>
        {
            foreach (var language in new[] { "Japanese", "Korean" })
            foreach (var separate in new[] { false, true })
            foreach (var choice in new[] { 3, 0, 1, 2 })
            {
                var text = language == "Japanese" ? "にほんごの歌詞" : "한국어가사";
                var handler = PronunciationFixture(PronunciationJson("[00:01]" + text), Wiki(language == "Japanese" ? "日语" : "韩语"));
                var plugin = PronunciationPlugin(handler, "\"" + language + "Text\":" + choice);
                SetConfig(plugin, "{\"Format\":1,\"TwoLineTranslation\":" + (separate ? "true" : "false") + ",\"" + language + "Text\":" + choice + "}");
                var join = separate ? "\n" : " / ";
                var expected = choice == 3 ? "[00:01]" + text : "[00:01.000]" + text + join +
                    (choice == 0 ? "译文" : choice == 1 ? "roman" : "roman" + join + "译文");
                Equal(expected, Lookup(plugin));
                Equal(choice == 0 ? 1 : 2, handler.Calls.Count);
            }
        });
        Test("Japanese lyrics-only preserves original metadata and timestamp spelling", () =>
        {
            const string original = "[offset:37]\n[ar:Singer]\n[00:01.0]にほんごの歌詞";
            var handler = PronunciationFixture(PronunciationJson(original), Wiki("日语"));
            Equal(original, Lookup(PronunciationPlugin(handler, "\"JapaneseText\":3,\"KoreanText\":2")));
            Equal(2, handler.Calls.Count);
        });
        Test("Korean lyrics-only does not alter the Japanese translation preference", () =>
        {
            foreach (var korean in new[] { false, true })
            {
                var original = korean ? "[00:01]한국어가사" : "[00:01]にほんごの歌詞";
                var handler = PronunciationFixture(PronunciationJson(original), Wiki(korean ? "韩语" : "日语"));
                Equal(korean ? original : "[00:01.000]にほんごの歌詞\n译文",
                    Lookup(PronunciationPlugin(handler, "\"JapaneseText\":0,\"KoreanText\":3")));
            }
        });
        Test("Lyrics-only requests a service label even without pronunciation or distinctive script", () =>
        {
            foreach (var language in new[] { "Japanese", "Korean" })
            {
                var handler = PronunciationFixture(PronunciationJson("[00:01]English words", roma: null), Wiki(language == "Japanese" ? "日语" : "韩语"));
                Equal("[00:01]English words", Lookup(PronunciationPlugin(handler, "\"" + language + "Text\":3")));
                Equal(2, handler.Calls.Count);
                Equal(1, handler.Calls.Count(c => c.Contains("/about/")));
            }
        });
        Test("Lyrics-only works in the same-line global format too", () =>
        {
            var plugin = PronunciationPlugin(PronunciationFixture(PronunciationJson(), Wiki("日语")), "\"JapaneseText\":3");
            SetConfig(plugin, "{\"Format\":1,\"JapaneseText\":3}");
            Equal("[00:01]にほんごの歌詞", Lookup(plugin));
        });
        Test("Legacy global-only modes migrate without reviving disabled language overrides", () =>
        {
            foreach (var format in new[] { 0, 2 })
            {
                var handler = PronunciationFixture(PronunciationJson(), Wiki("日语"));
                var plugin = PronunciationPlugin(handler, "\"JapaneseText\":3");
                SetConfig(plugin, "{\"Format\":" + format + ",\"JapaneseText\":3,\"KoreanText\":3}");
                Equal(format == 0 ? "[00:01]にほんごの歌詞" : "[00:01.000]にほんごの歌詞\n译文", Lookup(plugin));
                Equal(1, handler.Calls.Count);
            }
        });
        Test("Lyrics-only stops compatible-listing retries once original lyrics are usable", () =>
        {
            var handler = new Fixture((request, _) => Json(request.RequestUri.AbsolutePath.Contains("search") ? SearchJson(TwoListings()) :
                request.RequestUri.AbsolutePath.Contains("/about/") ? Wiki("日语") : PronunciationJson(roma: null, translation: null)));
            var plugin = CreatePlugin(handler);
            SetConfig(plugin, "{\"Format\":1,\"TwoLineTranslation\":true,\"JapaneseText\":3}");
            Equal("[00:01]にほんごの歌詞", Lookup(plugin, "Singer", "Song", "Album"));
            Equal(3, handler.Calls.Count);
            Equal(1, handler.Calls.Count(c => !c.Contains("search") && !c.Contains("/about/")));
        });
        Test("Lyrics-only caches known service labels while retrieving lyrics afresh", () =>
        {
            var handler = PronunciationFixture(PronunciationJson("[00:01]English words", roma: null), Wiki("日语"));
            var plugin = PronunciationPlugin(handler, "\"JapaneseText\":3");
            Equal("[00:01]English words", Lookup(plugin));
            Equal("[00:01]English words", Lookup(plugin));
            Equal(3, handler.Calls.Count);
            Equal(1, handler.Calls.Count(c => c.Contains("/about/")));
        });
        Test("Unknown lyrics-only language retains translation rather than guessing from Han text", () =>
        {
            var handler = PronunciationFixture(PronunciationJson("[00:01]中文歌词", roma: null), "{\"code\":-460}");
            Equal("[00:01.000]中文歌词\n译文", Lookup(PronunciationPlugin(handler, "\"JapaneseText\":3,\"KoreanText\":3")));
            Equal(2, handler.Calls.Count);
        });
        Test("A stalled service label respects its deadline and lyrics-only script fallback", () =>
        {
            var handler = PronunciationFixture(PronunciationJson(roma: null), Wiki("日语"));
            handler.StallLanguage = true;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Equal("[00:01]にほんごの歌詞", Lookup(PronunciationPlugin(handler, "\"JapaneseText\":3")));
            True(watch.Elapsed < TimeSpan.FromSeconds(4));
            Equal(2, handler.Calls.Count);
        });
        Test("Default spelling hint replaces the disabled checkbox without losing the saved choice", () =>
        {
            var plugin = PronunciationPlugin(new Fixture((_, __) => Json(Lyrics)), "\"UseLegacyMatch\":false,\"Fuzzy\":true");
            using (var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-32000, -32000) })
            using (var panel = new Panel { Dock = DockStyle.Fill })
            {
                form.Controls.Add(panel);
                plugin.Configure(panel.Handle); form.Show(); Application.DoEvents();
                var parent = panel.Controls.OfType<CheckBox>().Single(c => c.Text == "Customize spelling matching");
                var child = panel.Controls.OfType<CheckBox>().Single(c => c.Text == "Allow small spelling differences");
                var hint = panel.Controls.OfType<Label>().Single(c => c.Name == "SpellingDefault");
                True(hint.Visible); True(!child.Visible); True(!child.Enabled); True(child.Checked);
                parent.Checked = true;
                True(!hint.Visible); True(child.Visible); True(child.Enabled); True(child.Checked);
                child.Checked = false; parent.Checked = false;
                True(hint.Visible); True(!child.Visible);
                parent.Checked = true;
                True(child.Visible); True(!child.Checked);
                plugin.SaveSettings(); form.Close();
            }
            var saved = JObject.Parse(File.ReadAllText(Path.Combine(_directories[plugin], "netease_config")));
            Equal(true, (bool)saved["UseLegacyMatch"]); Equal(false, (bool)saved["Fuzzy"]);
        });
    }
}
