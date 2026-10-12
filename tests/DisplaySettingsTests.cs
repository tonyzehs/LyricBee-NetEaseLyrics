using MusicBeePlugin;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static partial class Program
{
    private static CheckBox DisplayCheck(Panel panel, string name) => panel.Controls.OfType<CheckBox>().Single(c => c.Name == name);
    private static ComboBox DisplayCombo(Panel panel, string name) => panel.Controls.OfType<ComboBox>().Single(c => c.Name == name);
    private static string ModernSettings(bool translation = true, bool pronunciation = false, bool separate = true, string extra = "") =>
        "{\"DisplaySettingsVersion\":1,\"ShowTranslation\":" + translation.ToString().ToLowerInvariant() +
        ",\"ShowPronunciation\":" + pronunciation.ToString().ToLowerInvariant() + ",\"SeparateLines\":" + separate.ToString().ToLowerInvariant() +
        ",\"InterfaceLanguage\":1" + (extra.Length == 0 ? "" : "," + extra) + "}";

    private static void DisplaySettingsTests()
    {
        Test("Fresh defaults include original and translation with pronunciation off and no overrides", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                True(DisplayCheck(panel, "ShowTranslation").Checked);
                True(!DisplayCheck(panel, "ShowPronunciation").Checked);
                Equal(0, DisplayCombo(panel, "OutputLayout").SelectedIndex);
                foreach (var language in new[] { "Cantonese", "Japanese", "Korean", "English" }) Equal(0, LanguageControl(panel, language).SelectedIndex);
                True(!panel.Controls.OfType<CheckBox>().Any(c => c.Name.Contains("Original")));
                var heading = panel.Controls.OfType<Label>().Single(c => c.Name == "LanguageOverridesHeading");
                True(panel.Controls.OfType<ComboBox>().Where(c => c.Name.EndsWith("Content")).All(c => c.Top > heading.Bottom));
            }
        });
        foreach (var separate in new[] { true, false })
        foreach (var translation in new[] { true, false })
        foreach (var pronunciation in new[] { true, false })
        foreach (var language in new[] { "Cantonese", "Japanese", "Korean", "English", "Other" })
        {
            var layout = separate; var trans = translation; var roma = pronunciation; var lang = language;
            Test("Independent defaults " + lang + " translation=" + trans + " pronunciation=" + roma + " separate=" + layout, () =>
            {
                var label = lang == "Other" ? "法语" : lang;
                var handler = PronunciationFixture(PronunciationJson("[00:01]Original", translation: lang == "Cantonese" ? null : "[00:01]译文"), Wiki(label));
                var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
                SetConfig(plugin, ModernSettings(trans, roma, layout));
                var includeRoma = roma && lang != "English" && lang != "Other";
                var includeTranslation = trans && lang != "Cantonese";
                var expected = !includeTranslation && !includeRoma ? "[00:01]Original" : "[00:01.000]Original" +
                    (includeRoma ? (layout ? "\n" : " / ") + "roman" : "") +
                    (includeTranslation ? (layout ? "\n" : " / ") + "译文" : "");
                Equal(expected, Lookup(plugin));
                Equal(roma ? 2 : 1, handler.Calls.Count);
            });
        }
        foreach (var language in new[] { "Cantonese", "Japanese", "Korean" })
        foreach (var value in new[] { 3, 0, 1, 2 })
        {
            var lang = language; var choice = language == "Cantonese" ? (value == 0 ? 3 : value == 2 ? 1 : value) : value;
            Test(lang + " override replaces default independently: " + choice, () =>
            {
                var handler = PronunciationFixture(PronunciationJson("[00:01]Original"), Wiki(lang));
                var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
                SetConfig(plugin, ModernSettings(false, false, true, "\"" + lang + "Override\":" + choice));
                var expected = choice == 3 ? "[00:01]Original" : "[00:01.000]Original\n" +
                    (choice == 0 ? "译文" : choice == 1 ? "roman" : "roman\n译文");
                Equal(expected, Lookup(plugin));
                True(handler.Calls.Count <= 2);
            });
        }
        Test("English translation opt-out does not suppress French or guess English from Latin text", () =>
        {
            foreach (var language in new[] { "英语", "法语", "英语,法语", "unknown" })
            {
                var handler = PronunciationFixture(PronunciationJson("[00:01]Ma Meilleure Ennemie"),
                    language == "unknown" ? "{\"code\":-460}" : Wiki(language.Split(',')));
                var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
                SetConfig(plugin, ModernSettings(extra: "\"EnglishOverride\":3"));
                Equal(language == "英语" ? "[00:01]Ma Meilleure Ennemie" : "[00:01.000]Ma Meilleure Ennemie\n译文", Lookup(plugin));
                Equal(2, handler.Calls.Count);
            }
        });
        Test("English translation override can enable translation when the default disables it", () =>
        {
            var handler = PronunciationFixture(PronunciationJson("[00:01]English words", roma: null), Wiki("en"));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
            SetConfig(plugin, ModernSettings(false, extra: "\"EnglishOverride\":0"));
            Equal("[00:01.000]English words\n译文", Lookup(plugin)); Equal(2, handler.Calls.Count);
        });
        Test("English controls expose follow-default and translation choices without pronunciation", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, ModernSettings());
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                Equal("Follow default|Lyrics|Lyrics + Translation", string.Join("|", LanguageControl(panel, "English").Items.Cast<string>()));
            }
        });
        Test("Language selector is visibly bilingual in either interface and Cantonese follows Japanese and Korean", () =>
        {
            foreach (var ui in new[] { 1, 2 })
            {
                var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
                SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":" + ui));
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    var caption = panel.Controls.OfType<Label>().Single(c => c.Name == "InterfaceLanguageLabel");
                    True(caption.Text.Contains("界面语言") && caption.Text.Contains("Interface language"));
                    True(DisplayCombo(panel, "InterfaceLanguage").Left > caption.Right);
                    Equal("JapaneseContent|KoreanContent|CantoneseContent|EnglishContent", string.Join("|", panel.Controls.OfType<ComboBox>()
                        .Where(c => c.Name.EndsWith("Content")).OrderBy(c => c.Top).Select(c => c.Name)));
                    var items = LanguageControl(panel, "Cantonese").Items.Cast<string>().ToArray();
                    Equal(ui == 1 ? "Follow default|Lyrics|Lyrics + Pronunciation" : "使用默认设置|歌词|歌词 + 发音", string.Join("|", items));
                }
            }
        });
        Test("Cantonese choices save and reopen without mapping pronunciation to translation", () =>
        {
            var handler = PronunciationFixture(PronunciationJson("[00:01]粤语歌词"), Wiki("粤语"));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
            SetConfig(plugin, ModernSettings());
            foreach (var index in new[] { 0, 1, 2 })
            {
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle); LanguageControl(panel, "Cantonese").SelectedIndex = index; plugin.SaveSettings();
                }
                var saved = JObject.Parse(File.ReadAllText(Path.Combine(_directories[plugin], "netease_config")));
                if (index == 0) Equal(JTokenType.Null, saved["CantoneseOverride"].Type);
                else Equal(index == 1 ? 3 : 1, (int)saved["CantoneseOverride"]);
                plugin.ReadConfig();
                using (var panel = new Panel()) { plugin.Configure(panel.Handle); Equal(index, LanguageControl(panel, "Cantonese").SelectedIndex); }
                if (index > 0) Equal(index == 1 ? "[00:01]粤语歌词" : "[00:01.000]粤语歌词\nroman", Lookup(plugin));
            }
        });
        Test("Obsolete Cantonese translation settings migrate without losing pronunciation or other settings", () =>
        {
            foreach (var choice in new[] { 0, 2 })
            {
                var handler = PronunciationFixture(PronunciationJson("[00:01]粤语歌词"), Wiki("粤语"));
                var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
                SetConfig(plugin, ModernSettings(extra: "\"CantoneseOverride\":" + choice + ",\"AllowInsecureHttp\":false,\"JapaneseOverride\":2"));
                using (var panel = new Panel())
                {
                    plugin.Configure(panel.Handle);
                    Equal(choice == 0 ? 1 : 2, LanguageControl(panel, "Cantonese").SelectedIndex);
                    Equal(4, LanguageControl(panel, "Japanese").SelectedIndex);
                    True(!DisplayCheck(panel, "AllowHttpFallback").Checked);
                }
                Equal(choice == 0 ? "[00:01]粤语歌词" : "[00:01.000]粤语歌词\nroman", Lookup(plugin));
            }
        });
        Test("Changing interface language preserves unsaved content layout and network choices", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, ModernSettings());
            using (var panel = new Panel { Size = new Size(560, 455) })
            {
                plugin.Configure(panel.Handle);
                DisplayCheck(panel, "ShowTranslation").Checked = false;
                DisplayCheck(panel, "ShowPronunciation").Checked = true;
                LanguageControl(panel, "Japanese").SelectedIndex = 4;
                DisplayCombo(panel, "OutputLayout").SelectedIndex = 1;
                DisplayCheck(panel, "AllowHttpFallback").Checked = false;
                DisplayCombo(panel, "InterfaceLanguage").SelectedIndex = 2;
                Equal("语言覆盖设置", panel.Controls.OfType<Label>().Single(c => c.Name == "LanguageOverridesHeading").Text);
                True(!DisplayCheck(panel, "ShowTranslation").Checked); True(DisplayCheck(panel, "ShowPronunciation").Checked);
                Equal(4, LanguageControl(panel, "Japanese").SelectedIndex); Equal(1, DisplayCombo(panel, "OutputLayout").SelectedIndex);
                True(!DisplayCheck(panel, "AllowHttpFallback").Checked);
                plugin.SaveSettings();
            }
            plugin.ReadConfig();
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle); Equal(2, DisplayCombo(panel, "InterfaceLanguage").SelectedIndex);
                Equal(4, LanguageControl(panel, "Japanese").SelectedIndex); True(!DisplayCheck(panel, "AllowHttpFallback").Checked);
            }
        });
        Test("Automatic interface language follows UI culture with an explicit choice taking priority", () =>
        {
            True(SettingsText.IsChinese(NeteaseConfig.UiLanguage.Automatic, CultureInfo.GetCultureInfo("zh-TW")));
            True(SettingsText.IsChinese(NeteaseConfig.UiLanguage.Automatic, CultureInfo.GetCultureInfo("zh-CN")));
            True(!SettingsText.IsChinese(NeteaseConfig.UiLanguage.Automatic, CultureInfo.GetCultureInfo("ja-JP")));
            True(!SettingsText.IsChinese(NeteaseConfig.UiLanguage.English, CultureInfo.GetCultureInfo("zh-CN")));
            True(SettingsText.IsChinese(NeteaseConfig.UiLanguage.SimplifiedChinese, CultureInfo.GetCultureInfo("en-US")));
        });
        Test("Malformed display fields reset independently and preserve HTTP opt-out", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, "{\"DisplaySettingsVersion\":1,\"ShowTranslation\":[],\"ShowPronunciation\":{},\"SeparateLines\":\"bad\",\"InterfaceLanguage\":99,\"JapaneseOverride\":99999999999999999999,\"KoreanOverride\":true,\"EnglishOverride\":2,\"AllowInsecureHttp\":false}");
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                True(DisplayCheck(panel, "ShowTranslation").Checked); True(!DisplayCheck(panel, "ShowPronunciation").Checked);
                True(!DisplayCheck(panel, "AllowHttpFallback").Checked);
                Equal(0, DisplayCombo(panel, "InterfaceLanguage").SelectedIndex);
                foreach (var lang in new[] { "Japanese", "Korean", "English" }) Equal(0, LanguageControl(panel, lang).SelectedIndex);
            }
        });
        Test("Follow-default persists as null and follows subsequent default changes", () =>
        {
            var handler = PronunciationFixture(PronunciationJson(), Wiki("日语"));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
            SetConfig(plugin, ModernSettings(extra: "\"JapaneseOverride\":3"));
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle); LanguageControl(panel, "Japanese").SelectedIndex = 0;
                DisplayCheck(panel, "ShowPronunciation").Checked = true; plugin.SaveSettings();
            }
            var saved = JObject.Parse(File.ReadAllText(Path.Combine(_directories[plugin], "netease_config")));
            Equal(JTokenType.Null, saved["JapaneseOverride"].Type);
            True((int)saved["Format"] >= 0 && (int)saved["Format"] <= 2);
            plugin.ReadConfig(); Equal("[00:01.000]にほんごの歌詞\nroman\n译文", Lookup(plugin));
        });
        Test("Saving legacy settings preserves a byte-identical backup once for rollback", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            const string old = "{\"Format\":2,\"AllowInsecureHttp\":false,\"JapaneseText\":2}";
            SetConfig(plugin, old);
            var path = Path.Combine(_directories[plugin], "netease_config");
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle);
                True(DisplayCheck(panel, "ShowTranslation").Checked);
                Equal(0, LanguageControl(panel, "Japanese").SelectedIndex);
                plugin.SaveSettings();
                Equal(old, File.ReadAllText(path + ".pre-display.bak"));
                DisplayCheck(panel, "ShowTranslation").Checked = false; plugin.SaveSettings();
                Equal(old, File.ReadAllText(path + ".pre-display.bak"));
            }
            plugin.ReadConfig();
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle); True(!DisplayCheck(panel, "ShowTranslation").Checked);
                True(!DisplayCheck(panel, "AllowHttpFallback").Checked);
            }
        });
        Test("An override remains active while default extras are disabled and layout changes", () =>
        {
            var handler = PronunciationFixture(PronunciationJson(), Wiki("日语"));
            var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
            SetConfig(plugin, ModernSettings(extra: "\"JapaneseOverride\":2"));
            using (var panel = new Panel())
            {
                plugin.Configure(panel.Handle); DisplayCheck(panel, "ShowTranslation").Checked = false;
                DisplayCheck(panel, "ShowPronunciation").Checked = false;
                DisplayCombo(panel, "OutputLayout").SelectedIndex = 1; plugin.SaveSettings();
            }
            plugin.ReadConfig(); Equal("[00:01.000]にほんごの歌詞 / roman / 译文", Lookup(plugin));
        });
        Test("New three-line preferences fall back independently without dropping original lyrics", () =>
        {
            foreach (var extras in new[] { 0, 1, 2, 3 })
            {
                var handler = PronunciationFixture(PronunciationJson("[00:01]Original", (extras & 1) != 0 ? "[00:01]roman" : null,
                    (extras & 2) != 0 ? "[00:01]译文" : null), Wiki("日语"));
                var plugin = CreatePlugin(handler, (_, tag) => tag == Plugin.MetaDataType.Custom10 ? "netease=101" : null);
                SetConfig(plugin, ModernSettings(true, true));
                Equal(extras == 0 ? "[00:01]Original" : "[00:01.000]Original" +
                    ((extras & 1) != 0 ? "\nroman" : "") + ((extras & 2) != 0 ? "\n译文" : ""), Lookup(plugin));
                True(handler.Calls.Count <= 2);
            }
        });
        Test("All language overrides and bilingual labels fit the panel at common font sizes", () =>
        {
            foreach (var ui in new[] { 1, 2 })
            foreach (var width in new[] { 450, 580 })
            foreach (var size in new[] { 9f, 11f })
            {
                var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
                SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":" + ui));
                using (var form = new Form { ShowInTaskbar = false, Location = new Point(-32000, -32000), StartPosition = FormStartPosition.Manual })
                using (var font = new Font("Segoe UI", size))
                using (var panel = new Panel { Size = new Size(width, 455), Font = font })
                {
                    form.Controls.Add(panel); plugin.Configure(panel.Handle); form.Show(); Application.DoEvents();
                    foreach (var control in panel.Controls.Cast<Control>().Where(c => c.Visible))
                    {
                        if (control.Right > panel.ClientSize.Width || control.Bottom > panel.ClientSize.Height)
                            throw new Exception("Outside panel: " + control.Name + " ui=" + ui + " width=" + width + " font=" + size + " bounds=" + control.Bounds);
                        if (control is CheckBox || (control is Label && ((Label)control).AutoSize))
                            if (control.PreferredSize.Width > control.Width)
                                throw new Exception("Clipped text: " + control.Name + " ui=" + ui + " font=" + size);
                    }
                    True(panel.Controls.OfType<ComboBox>().All(c => c.Enabled));
                    form.Close();
                }
            }
        });
    }

    private static int SettingsPreview(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var ui in new[] { 1, 2 })
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":" + ui));
            using (var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000) })
            using (var panel = new Panel { Size = new Size(560, 455), BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 10f) })
            using (var bitmap = new Bitmap(560, 455))
            {
                form.ClientSize = panel.Size; form.Controls.Add(panel); plugin.Configure(panel.Handle);
                form.Show(); Application.DoEvents(); panel.DrawToBitmap(bitmap, panel.ClientRectangle);
                bitmap.Save(Path.Combine(directory, ui == 1 ? "settings-en.png" : "settings-zh-CN.png")); form.Close();
            }
        }
        return 0;
    }
}
