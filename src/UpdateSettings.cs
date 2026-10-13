using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        internal UpdateChecker UpdateChecker { get; set; }
        internal Action<string> ReleasePageOpener { get; set; } = url =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        private void AddUpdateControls(SettingsLayout layout, Func<SettingsLabel, string> text)
        {
            var panel = layout.Host;
            var check = new Button { Name = "CheckForUpdates", Text = text(SettingsLabel.CheckUpdates),
                Margin = new Padding(0, 0, 12, 0), AutoSize = true };
            var found = new Button { Name = "UpdateFound", Text = text(SettingsLabel.UpdateFound),
                Margin = Padding.Empty, AutoSize = true, Visible = false };
            var status = new Label { Name = "UpdateStatus", Text = string.Format(text(SettingsLabel.UpdateInstalled), BuildVersion),
                AutoSize = true, ForeColor = panel.ForeColor };
            var buttons = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true, Margin = Padding.Empty, Padding = Padding.Empty };
            buttons.Controls.Add(check); buttons.Controls.Add(found); layout.Add(buttons, 7, 29);
            layout.Add(status, 6, 36);
            CancellationTokenSource pending = null;
            UpdateResult result = null;
            var failedText = text(SettingsLabel.UpdateFailed);
            check.Disposed += (sender, e) => pending?.Cancel();
            check.Click += async (sender, e) =>
            {
                if (pending != null) return;
                CancellationTokenSource cancellation = null;
                try
                {
                    cancellation = new CancellationTokenSource(); pending = cancellation;
                    check.Enabled = false; found.Visible = false; result = null;
                    status.Text = text(SettingsLabel.UpdateChecking);
                    var checkedResult = await UpdateChecker.CheckAsync(BuildVersion, cancellation.Token);
                    if (check.IsDisposed || cancellation.IsCancellationRequested) return;
                    result = checkedResult;
                    status.Text = !result.Succeeded ? text(result.RateLimited ? SettingsLabel.UpdateRateLimited : SettingsLabel.UpdateFailed) :
                        string.Format(text(result.UpdateAvailable ? SettingsLabel.UpdateAvailable : SettingsLabel.UpdateCurrent), result.LatestVersion);
                    found.Visible = result.Succeeded && result.UpdateAvailable;
                }
                catch
                {
                    Trace("Update check failed: internal failure");
                    try { if (!check.IsDisposed) { found.Visible = false; status.Text = failedText; } } catch { }
                }
                finally
                {
                    pending = null;
                    try { cancellation?.Dispose(); if (!check.IsDisposed) check.Enabled = true; } catch { }
                }
            };
            found.Click += (sender, e) =>
            {
                if (result?.UpdateAvailable != true || !result.Succeeded) return;
                try { ReleasePageOpener(result.ReleasePage); }
                catch { status.Text = text(SettingsLabel.UpdateOpenFailed); }
            };
        }
    }
}
