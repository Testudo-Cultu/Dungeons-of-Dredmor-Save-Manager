//using System;
//using System.IO;
using System.IO.Compression;
//using System.Linq;
using System.Text.Json;
//using System.Windows.Forms;
//using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace SimpleBackup
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new BackupForm());
        }
    }

    public sealed class BackupForm : Form
    {
        // Controls
        private readonly ToolTip toolTip = new();
        private readonly TextBox txtSource = new() { Width = 310 };
        private readonly Button btnBrowseSource = new() { Text = "Browse…" };
        private readonly TextBox txtDest = new() { Width = 310 };
        private readonly Button btnBrowseDest = new() { Text = "Browse…" };
        private readonly NumericUpDown numInterval = new() { Minimum = 1, Maximum = 1440, Value = 5 };
        private readonly CheckBox chkRotate = new() { Text = "Max backups", Checked = true };
        private readonly NumericUpDown numMaxBackups = new() { Minimum = 1, Maximum = 1000, Value = 10 };
        private readonly ComboBox cboCompression = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Button btnStartStop = new() { Text = "Start" };
        private readonly RichTextBox logBox = new()
        {
            Width = 510,
            Height = 230,
            ReadOnly = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom |
                     AnchorStyles.Left | AnchorStyles.Right
        };

        private readonly System.Windows.Forms.Timer backupTimer = new();
        private readonly string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string cfgPath;
        private readonly string logPath;
        private StreamWriter? logWriter;

        private const int ExpectedBackupNameLength = 26;               // “Backup_yyyyMMdd_HHmmss.zip”

        private sealed record Config(string SourceFolder, string DestFolder, int IntervalMinutes,
                                     bool RotationEnabled, int MaxBackups, string Compression);

        public BackupForm()
        {
            Text = "Dungeons of Dredmor Save Manager";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 560; Height = 480;

            cfgPath = Path.Combine(exeDir, "config.json");
            logPath = Path.Combine(exeDir, "backup.log");

            toolTip.AutoPopDelay = 25000;

            /* layout (same as before) */
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 8 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            int r = 0;
            table.Controls.Add(new Label { Text = "Source folder:" }, 0, 0);
            table.Controls.Add(txtSource, 1, r);
            table.Controls.Add(btnBrowseSource, 2, r++);
            toolTip.SetToolTip(this.btnBrowseSource, "This is the folder to make backups of. Your Dredmor saves should be in the Gaslamp Games folder within Documents. I personally find it easiest to select the entire Dungeons of Dredmor folder.");
            toolTip.SetToolTip(this.txtSource, @"“C:\Users\<YourUsername>\Documents\Gaslamp Games\Dungeons of Dredmor” is typically where you will find your saves, but this program can be used for other stuff too.");

            table.Controls.Add(new Label { Text = "Destination folder:" }, 0, 1);
            table.Controls.Add(txtDest, 1, r);
            table.Controls.Add(btnBrowseDest, 2, r++);
            toolTip.SetToolTip(this.txtDest, "I recommend you make a folder specifically for your backups. NEVER select a folder used for anything other than this program. If you do, the program will run badly and throw safeguard warnings at you, or at worst, delete something else in the folder.");
            toolTip.SetToolTip(this.btnBrowseDest, "This is where backups will be put. I put in a lot of safeguards to prevent the deletion of unrelated data, but please ensure that you select a folder that isn't used for anything important.");

            table.Controls.Add(new Label { Text = "Interval (min):" }, 0, 2);
            table.Controls.Add(numInterval, 1, r++);
            toolTip.SetToolTip(this.numInterval, "Minutes to wait before creating the next backup of the designated source folder to the destination folder.");

            table.Controls.Add(numMaxBackups, 1, r++);
            toolTip.SetToolTip(this.numMaxBackups, "If enabled, this is the maximum number of backups allowed before older backups begin getting deleted.");
            table.Controls.Add(chkRotate, 0, 3);
            toolTip.SetToolTip(this.chkRotate, "Automatically delete the oldest save when creating a new save, keeping only the designated number of backups.");

            table.Controls.Add(new Label { Text = "Compression:" }, 0, 4);
            table.Controls.Add(cboCompression, 1, 4);
            cboCompression.Items.AddRange(new[] { "Optimal", "Fastest", "NoCompression", "SmallestSize" });
            toolTip.SetToolTip(this.cboCompression, "These are .NET's verbatim CompressionLevel enums. This is an optional control for what kind of compression to use for backups. I recommend Optimal, but SmallestSize might help slightly if you're limited on storage.");

            table.Controls.Add(btnStartStop, 2, 4);
            toolTip.SetToolTip(this.btnStartStop, "Toggle for the program to begin or stop making backups. I suggest setting all the controls beforehand. Program might freeze for a few seconds when clicked, so don't be alarmed.");

            table.SetColumnSpan(logBox, 4);
            table.Controls.Add(logBox, 0, 5);
            Controls.Add(table);

            // Events
            btnBrowseSource.Click += (s, e) => PickFolder(txtSource);
            btnBrowseDest.Click += (s, e) => PickFolder(txtDest);
            btnStartStop.Click += BtnStartStop_Click;
            chkRotate.CheckedChanged += (s, e) =>
                numMaxBackups.Enabled = chkRotate.Checked;
            backupTimer.Tick += (s, e) => RunBackup();

            LoadConfig();
            numMaxBackups.Enabled = chkRotate.Checked;
        }

        private void PickFolder(TextBox target) { using var d = new FolderBrowserDialog { SelectedPath = target.Text }; if (d.ShowDialog() == DialogResult.OK) target.Text = d.SelectedPath; }
        private void BtnStartStop_Click(object? s, EventArgs e)
        {
            if (!backupTimer.Enabled)
            {
                if (!Directory.Exists(txtSource.Text) || !Directory.Exists(txtDest.Text)) { MessageBox.Show("Select valid folders."); return; }
                backupTimer.Interval = (int)numInterval.Value * 60 * 1000; backupTimer.Start(); btnStartStop.Text = "Stop";
                OpenLogWriter(); Log($"Backup timer started ({numInterval.Value} min)."); RunBackup();
            }
            else { backupTimer.Stop(); btnStartStop.Text = "Start"; Log("Backup timer stopped."); logWriter?.Dispose(); }
            SaveConfig();
        }
        private void RunBackup()
        {
            try
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string zipName = $"Backup_{stamp}.zip";
                string fullPath = Path.Combine(txtDest.Text, zipName);
                var level = (CompressionLevel)Enum.Parse(typeof(CompressionLevel), cboCompression.SelectedItem!.ToString()!);
                ZipFile.CreateFromDirectory(txtSource.Text, fullPath, level, false);
                Log($"✓  {zipName} created.");
                if (chkRotate.Checked) RotateBackups();
            }
            catch (Exception ex) { Log($"✗  ERROR: {ex.Message}"); }
        }

        // Backup rotation logic to prevent storage use from getting out of hand. To prevent freak accidents, it checks the file name and file name length to make sure it isn't deleting the wrong data.
        private void RotateBackups()
        {
            int maxBackups = (int)numMaxBackups.Value;
            var all = Directory.GetFiles(txtDest.Text, "Backup_*.zip");

            // Split into “valid” (length == 26) and “suspicious”
            var valid = all.Where(p => Path.GetFileName(p).Length == ExpectedBackupNameLength)
                             .OrderBy(p => p)      // lexicographic == chronological
                             .ToArray();
            var suspect = all.Except(valid).ToArray();

            if (suspect.Length > 0)
            {
                Log($"⚠  Found {suspect.Length} file(s) with the Backup_ prefix but unexpected length; not touched.");
                MessageBox.Show(
                    $"Found {suspect.Length} ZIP file(s) matching Backup_* but the names are not {ExpectedBackupNameLength} characters long.\n"
                  + "They were skipped for safety. See log for details.",
                    "Suspicious backup names", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (valid.Length <= maxBackups) return;

            int deleteCount = valid.Length - maxBackups;

            // This is to prevent automatic deletion of your backups if you accidentally set the max backups to a much lower number
            if (deleteCount > 1)
            {
                var resp = MessageBox.Show(
                    $"Rotation would delete {deleteCount} old backup(s).\nProceed?",
                    "Confirm deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (resp != DialogResult.Yes) { Log("Rotation cancelled."); return; }
            }

            for (int i = 0; i < deleteCount; i++)
            {
                string f = valid[i];
                try { File.Delete(f); Log($"␡  Deleted old backup: {Path.GetFileName(f)}"); }
                catch (Exception ex) { Log($"⚠  Could not delete {Path.GetFileName(f)}: {ex.Message}"); }
            }
        }

        // Config and log stuff so your settings are retained and this program is a bit more clear about what it's doing
        private void Log(string line) { string msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}"; logBox.AppendText(msg + "\r\n"); logWriter?.WriteLine(msg); logWriter?.Flush(); }
        private void OpenLogWriter() { logWriter ??= new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read)) { AutoFlush = true }; }
        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(cfgPath)) return; var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(cfgPath)); if (cfg == null) return;
                txtSource.Text = cfg.SourceFolder; txtDest.Text = cfg.DestFolder; numInterval.Value = cfg.IntervalMinutes;
                chkRotate.Checked = cfg.RotationEnabled; numMaxBackups.Value = cfg.MaxBackups; cboCompression.SelectedItem = cfg.Compression;
            }
            catch { }
            finally { if (cboCompression.SelectedItem == null) cboCompression.SelectedItem = "Optimal"; }
        }
        private void SaveConfig()
        {
            var cfg = new Config(txtSource.Text, txtDest.Text, (int)numInterval.Value, chkRotate.Checked, (int)numMaxBackups.Value,
                               cboCompression.SelectedItem?.ToString() ?? "Optimal");
            try { File.WriteAllText(cfgPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true })); }
            catch (Exception ex) { Log($"⚠  Could not save config: {ex.Message}"); }
        }
    }
}
