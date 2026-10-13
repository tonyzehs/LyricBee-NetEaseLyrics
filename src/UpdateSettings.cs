using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace MusicBeePlugin
{
    public partial class Plugin
    {
        internal UpdateChecker UpdateChecker { get; set; } = new UpdateChecker();
        internal Action<string> ReleasePageOpener { get; set; } = url =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        private Action AddUpdateControls(Panel panel, Func<SettingsLabel, string> text)
        {
            var check = new Button { Name = "CheckForUpdates", Text = text(SettingsLabel.CheckUpdates),
                Location = new Point(0, 430), AutoSize = true };
            var found = new Button { Name = "UpdateFound", Text = text(SettingsLabel.UpdateFound),
                Location = new Point(170, 430), AutoSize = true, Visible = false };
            var status = new Label { Name = "UpdateStatus", Text = string.Format(text(SettingsLabel.UpdateInstalled), BuildVersion),
                Location = new Point(0, 466), Size = new Size(430, 36), ForeColor = panel.ForeColor };
            panel.Controls.Add(check); panel.Controls.Add(found); panel.Controls.Add(status);
            CancellationTokenSource pending = null;
            UpdateResult result = null;
            check.Disposed += (sender, e) => pending?.Cancel();
            check.Click += async (sender, e) =>
            {
                if (pending != null) return;
                var cancellation = new CancellationTokenSource();
                pending = cancellation;
                check.Enabled = false; found.Visible = false; result = null;
                status.Text = text(SettingsLabel.UpdateChecking);
                try
                {
                    var checkedResult = await UpdateChecker.CheckAsync(BuildVersion, cancellation.Token);
                    if (check.IsDisposed || cancellation.IsCancellationRequested) return;
                    result = checkedResult;
                    status.Text = !result.Succeeded ? text(SettingsLabel.UpdateFailed) :
                        string.Format(text(result.UpdateAvailable ? SettingsLabel.UpdateAvailable : SettingsLabel.UpdateCurrent), result.LatestVersion);
                    found.Visible = result.Succeeded && result.UpdateAvailable;
                }
                finally
                {
                    pending = null; cancellation.Dispose();
                    if (!check.IsDisposed) check.Enabled = true;
                }
            };
            found.Click += (sender, e) =>
            {
                if (result?.UpdateAvailable != true || !result.Succeeded) return;
                try { ReleasePageOpener(result.ReleasePage); }
                catch { status.Text = text(SettingsLabel.UpdateOpenFailed); }
            };
            return () =>
            {
                found.Left = check.Right + 12;
                status.Width = Math.Max(200, panel.ClientSize.Width - 8);
            };
        }
    }
}
