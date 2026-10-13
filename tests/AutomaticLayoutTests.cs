using MusicBeePlugin;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static partial class Program
{
    private static void AutomaticLayoutTests()
    {
        Test("Automatic layout preserves row order and separates controls at 100 150 and 200 percent font scaling", () =>
        {
            foreach (var ui in new[] { 1, 2 })
            foreach (var scale in new[] { 1f, 1.5f, 2f })
            foreach (var width in new[] { 450, 580 })
            {
                var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
                SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":" + ui));
                plugin.UpdateChecker = new UpdateChecker(() => UpdateResponse(ReleaseJson()));
                using (var font = new Font("Segoe UI", 10f * scale))
                using (var form = new Form { AutoScaleMode = AutoScaleMode.None, ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000) })
                using (var panel = new Panel { Font = font, Size = new Size((int)(width * scale), (int)(655 * scale)) })
                {
                    form.ClientSize = panel.Size; form.Controls.Add(panel); plugin.Configure(panel.Handle); form.Show(); Application.DoEvents();
                    LanguageControl(panel, "Japanese").SelectedIndex = 4; LanguageControl(panel, "Korean").SelectedIndex = 4;
                    ToggleAdvanced(panel); ((Button)FindSetting(panel, "CheckForUpdates")).PerformClick();
                    PumpUpdate(() => FindSetting(panel, "CheckForUpdates").Enabled); Application.DoEvents();
                    True(panel.Controls.OfType<TableLayoutPanel>().Any()); True(panel.AutoScroll);
                    foreach (var control in SettingsControls(panel).Where(c => c.Visible))
                    {
                        var area = control.Parent is ScrollableControl scrolling && scrolling.AutoScroll ? scrolling.DisplayRectangle :
                            new Rectangle(Point.Empty, control.Parent.ClientSize);
                        if (control.Right > area.Right + 1 || control.Bottom > area.Bottom + 1)
                            throw new Exception("Clipped child " + control.Name + " ui=" + ui + " scale=" + scale + " bounds=" + control.Bounds + " parent=" + control.Parent.ClientSize);
                        if (control is Label label && !(label is LinkLabel))
                            True(label.GetPreferredSize(new Size(label.Width, 0)).Height <= label.Height + 1);
                    }
                    var names = new[] { "DefaultDisplayHeading", "OriginalAlways", "ShowTranslation", "OutputLayout",
                        "LanguageOverridesHeading", "JapaneseContent", "KoreanContent", "CantoneseContent", "EnglishContent",
                        "RefetchHint", "MatchingNetworkHeading", "SmallSpellingDifferences", "AllowHttpFallback",
                        "CheckForUpdates", "UpdateStatus", "AdvancedDiagnostics", "CopyDiagnosticReport", "DiagnosticPrivacyNote" };
                    var previousBottom = int.MinValue;
                    foreach (var name in names)
                    {
                        var control = FindSetting(panel, name);
                        var point = panel.PointToClient(control.PointToScreen(Point.Empty));
                        True(point.Y >= previousBottom); previousBottom = point.Y + control.Height;
                    }
                    var check = FindSetting(panel, "CheckForUpdates"); var view = FindSetting(panel, "UpdateFound");
                    True(view.Visible); True(!check.Bounds.IntersectsWith(view.Bounds));
                    form.Close();
                }
            }
        });
        Test("Narrow high-text-scale settings can scroll rather than truncate selected choices", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":1"));
            using (var font = new Font("Segoe UI", 20f))
            using (var form = new Form { AutoScaleMode = AutoScaleMode.None, ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000) })
            using (var panel = new Panel { Font = font, Size = new Size(450, 630) })
            {
                form.ClientSize = panel.Size; form.Controls.Add(panel); plugin.Configure(panel.Handle); form.Show(); Application.DoEvents();
                LanguageControl(panel, "Japanese").SelectedIndex = 4; ToggleAdvanced(panel); Application.DoEvents();
                var combo = LanguageControl(panel, "Japanese");
                var required = TextRenderer.MeasureText(combo.Text, combo.Font).Width + SystemInformation.VerticalScrollBarWidth;
                if (combo.Width < required) throw new Exception("Selected choice width=" + combo.Width + " required=" + required + " table=" + combo.Parent.Size);
                True(panel.AutoScroll && panel.DisplayRectangle.Width >= combo.Right);
                panel.ScrollControlIntoView(FindSetting(panel, "DiagnosticPrivacyNote"));
                True(panel.AutoScrollPosition.Y < 0); form.Close();
            }
        });
        Test("Expanded text reflows subsequent rows without saving a settings draft", () =>
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":1"));
            var saved = File.ReadAllText(Path.Combine(_directories[plugin], "netease_config"));
            using (var form = new Form { AutoScaleMode = AutoScaleMode.None, ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000) })
            using (var panel = new Panel { Size = new Size(450, 655) })
            {
                form.ClientSize = panel.Size; form.Controls.Add(panel); plugin.Configure(panel.Handle); form.Show(); Application.DoEvents();
                var note = (Label)FindSetting(panel, "RefetchHint");
                var before = FindSetting(panel, "MatchingNetworkHeading").Top;
                note.Text = string.Join(" ", Enumerable.Repeat(note.Text, 6)); Application.DoEvents();
                True(FindSetting(panel, "MatchingNetworkHeading").Top > before);
                True(FindSetting(panel, "MatchingNetworkHeading").Top >= note.Bottom);
                using (var enlarged = new Font("Segoe UI", 18f))
                {
                    panel.Font = enlarged; Application.DoEvents();
                    Equal(enlarged.SizeInPoints, FindSetting(panel, "MatchingNetworkHeading").Font.SizeInPoints);
                    True(FindSetting(panel, "MatchingNetworkHeading").Top >= note.Bottom);
                }
                Equal(saved, File.ReadAllText(Path.Combine(_directories[plugin], "netease_config"))); form.Close();
            }
        });
    }

    private static int ScaledSettingsPreview(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var ui in new[] { 1, 2 })
        foreach (var scale in new[] { 1f, 1.5f, 2f })
        {
            var plugin = CreatePlugin(new Fixture((_, __) => Json(Lyrics)));
            SetConfig(plugin, ModernSettings(extra: "\"InterfaceLanguage\":" + ui));
            plugin.UpdateChecker = new UpdateChecker(() => UpdateResponse(ReleaseJson()));
            using (var font = new Font("Segoe UI", 10f * scale))
            using (var form = new Form { AutoScaleMode = AutoScaleMode.None, ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000) })
            using (var panel = new Panel { Font = font, Size = new Size((int)(560 * scale), (int)(655 * scale)),
                BackColor = Color.FromArgb(45, 45, 45), ForeColor = Color.Gainsboro })
            using (var bitmap = new Bitmap(panel.Width, panel.Height))
            {
                form.ClientSize = panel.Size; form.Controls.Add(panel); plugin.Configure(panel.Handle); form.Show(); Application.DoEvents();
                ToggleAdvanced(panel); ((Button)FindSetting(panel, "CheckForUpdates")).PerformClick();
                PumpUpdate(() => FindSetting(panel, "CheckForUpdates").Enabled); panel.DrawToBitmap(bitmap, panel.ClientRectangle);
                bitmap.Save(Path.Combine(directory, "settings-" + (ui == 1 ? "en-" : "zh-CN-") + (int)(scale * 100) + ".png"));
                form.Close();
            }
        }
        return 0;
    }
}
