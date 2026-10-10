using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace PromptManagerSetup;

internal static class Program
{
    private const string AppName = "Prompt Manager";
    private const string ExeName = "PromptManager.exe";
    private const string PayloadResource = "PromptManagerSetup.Payload.zip";
    private const string InstallManifestName = "install-manifest.txt";
    private const string UninstallScriptName = "UninstallPromptManager.ps1";
    private const string UninstallCommandName = "Uninstall Prompt Manager.cmd";
    private const string InstallerVersion = "1.4.0";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var options = InstallOptions.FromArgs(args);
        if (options.Silent)
        {
            Installer.Install(options, null);
            return;
        }

        Application.Run(new SetupForm(options));
    }

    internal sealed class SetupForm : Form
    {
        private readonly TextBox _installPathTextBox = new();
        private readonly Button _browseButton = new();
        private readonly Button _installButton = new();
        private readonly Button _cancelButton = new();
        private readonly CheckBox _desktopShortcutCheckBox = new();
        private readonly CheckBox _startMenuShortcutCheckBox = new();
        private readonly CheckBox _launchCheckBox = new();
        private readonly ProgressBar _progressBar = new();
        private readonly Label _statusLabel = new();
        private readonly InstallOptions _options;

        public SetupForm(InstallOptions options)
        {
            _options = options;
            Text = "Prompt Manager Setup";
            using var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PromptManagerSetup.AppIcon.ico")
                ?? throw new InvalidOperationException("The installer icon is missing.");
            using var applicationIcon = new Icon(iconStream);
            Icon = (Icon)applicationIcon.Clone();
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(640, 430);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(242, 248, 248);

            BuildLayout();
        }

        private void BuildLayout()
        {
            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 82,
                BackColor = Color.FromArgb(128, 157, 176)
            };

            var titleLabel = new Label
            {
                Text = "Prompt Manager Setup",
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(33, 49, 61),
                AutoSize = true,
                Location = new Point(22, 16)
            };
            var subtitleLabel = new Label
            {
                Text = "Install the offline prompt library manager on this Windows PC.",
                ForeColor = Color.FromArgb(33, 49, 61),
                AutoSize = true,
                Location = new Point(24, 52)
            };
            header.Controls.Add(titleLabel);
            header.Controls.Add(subtitleLabel);
            Controls.Add(header);

            var content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24)
            };
            Controls.Add(content);

            var locationLabel = new Label
            {
                Text = "Install location",
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Location = new Point(24, 24)
            };
            content.Controls.Add(locationLabel);

            _installPathTextBox.Text = _options.InstallDirectory;
            _installPathTextBox.Location = new Point(24, 52);
            _installPathTextBox.Size = new Size(468, 27);
            content.Controls.Add(_installPathTextBox);

            _browseButton.Text = "Browse...";
            _browseButton.Location = new Point(502, 51);
            _browseButton.Size = new Size(90, 29);
            _browseButton.Click += (_, _) => BrowseInstallLocation();
            content.Controls.Add(_browseButton);

            _desktopShortcutCheckBox.Text = "Create desktop shortcut";
            _desktopShortcutCheckBox.Checked = _options.CreateDesktopShortcut;
            _desktopShortcutCheckBox.AutoSize = true;
            _desktopShortcutCheckBox.Location = new Point(24, 102);
            content.Controls.Add(_desktopShortcutCheckBox);

            _startMenuShortcutCheckBox.Text = "Create Start menu shortcut";
            _startMenuShortcutCheckBox.Checked = _options.CreateStartMenuShortcut;
            _startMenuShortcutCheckBox.AutoSize = true;
            _startMenuShortcutCheckBox.Location = new Point(24, 132);
            content.Controls.Add(_startMenuShortcutCheckBox);

            _launchCheckBox.Text = "Launch Prompt Manager after installation";
            _launchCheckBox.Checked = _options.LaunchAfterInstall;
            _launchCheckBox.AutoSize = true;
            _launchCheckBox.Location = new Point(24, 162);
            content.Controls.Add(_launchCheckBox);

            _progressBar.Location = new Point(24, 210);
            _progressBar.Size = new Size(568, 24);
            _progressBar.Minimum = 0;
            _progressBar.Maximum = 100;
            content.Controls.Add(_progressBar);

            _statusLabel.Text = "Ready to install.";
            _statusLabel.AutoSize = false;
            _statusLabel.Location = new Point(24, 244);
            _statusLabel.Size = new Size(568, 48);
            _statusLabel.ForeColor = Color.FromArgb(93, 110, 120);
            content.Controls.Add(_statusLabel);

            _installButton.Text = "Install";
            _installButton.Location = new Point(410, 316);
            _installButton.Size = new Size(88, 32);
            _installButton.Click += async (_, _) => await InstallAsync();
            content.Controls.Add(_installButton);

            _cancelButton.Text = "Cancel";
            _cancelButton.Location = new Point(504, 316);
            _cancelButton.Size = new Size(88, 32);
            _cancelButton.Click += (_, _) => Close();
            content.Controls.Add(_cancelButton);
        }

        private void BrowseInstallLocation()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose where Prompt Manager should be installed.",
                SelectedPath = _installPathTextBox.Text,
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _installPathTextBox.Text = dialog.SelectedPath;
            }
        }

        private async Task InstallAsync()
        {
            var installDirectory = _installPathTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                MessageBox.Show(this, "Choose an install location before continuing.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (Directory.Exists(installDirectory) && Directory.EnumerateFileSystemEntries(installDirectory).Any())
            {
                var answer = MessageBox.Show(
                    this,
                    "The selected folder already contains files. Prompt Manager files will be copied into this folder and existing files with the same names may be replaced.",
                    Text,
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);
                if (answer != DialogResult.OK)
                {
                    return;
                }
            }

            SetBusy(true);
            _options.InstallDirectory = installDirectory;
            _options.CreateDesktopShortcut = _desktopShortcutCheckBox.Checked;
            _options.CreateStartMenuShortcut = _startMenuShortcutCheckBox.Checked;
            _options.LaunchAfterInstall = _launchCheckBox.Checked;

            try
            {
                await Task.Run(() => Installer.Install(_options, ReportProgress));
                _statusLabel.Text = "Prompt Manager was installed successfully.";
                _progressBar.Value = 100;
                MessageBox.Show(this, "Prompt Manager was installed successfully.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "Installation failed: " + ex.Message;
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                SetBusy(false);
            }
        }

        private void ReportProgress(int percent, string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ReportProgress(percent, message));
                return;
            }

            _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, percent));
            _statusLabel.Text = message;
        }

        private void SetBusy(bool busy)
        {
            _installPathTextBox.Enabled = !busy;
            _browseButton.Enabled = !busy;
            _desktopShortcutCheckBox.Enabled = !busy;
            _startMenuShortcutCheckBox.Enabled = !busy;
            _launchCheckBox.Enabled = !busy;
            _installButton.Enabled = !busy;
            _cancelButton.Enabled = !busy;
        }
    }

    internal sealed class InstallOptions
    {
        public string InstallDirectory { get; set; } = GetDefaultInstallDirectory();
        public bool CreateDesktopShortcut { get; set; } = true;
        public bool CreateStartMenuShortcut { get; set; } = true;
        public bool LaunchAfterInstall { get; set; } = true;
        public bool RegisterUninstallEntry { get; set; } = true;
        public bool Silent { get; set; }

        public static InstallOptions FromArgs(string[] args)
        {
            var options = new InstallOptions();
            foreach (var arg in args)
            {
                if (arg.Equals("/S", StringComparison.OrdinalIgnoreCase) || arg.Equals("--silent", StringComparison.OrdinalIgnoreCase))
                {
                    options.Silent = true;
                }
                else if (arg.StartsWith("/DIR=", StringComparison.OrdinalIgnoreCase) || arg.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase))
                {
                    options.InstallDirectory = arg[(arg.IndexOf('=') + 1)..].Trim('"');
                }
                else if (arg.Equals("/NODESKTOP", StringComparison.OrdinalIgnoreCase))
                {
                    options.CreateDesktopShortcut = false;
                }
                else if (arg.Equals("/NOSTARTMENU", StringComparison.OrdinalIgnoreCase))
                {
                    options.CreateStartMenuShortcut = false;
                }
                else if (arg.Equals("/NOLAUNCH", StringComparison.OrdinalIgnoreCase))
                {
                    options.LaunchAfterInstall = false;
                }
                else if (arg.Equals("/NOREGISTRY", StringComparison.OrdinalIgnoreCase))
                {
                    options.RegisterUninstallEntry = false;
                }
            }

            return options;
        }

        private static string GetDefaultInstallDirectory()
        {
            using var existing = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PromptManager");
            if (existing?.GetValue("InstallLocation") is string installedPath && File.Exists(Path.Combine(installedPath, ExeName)))
                return installedPath;
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (string.IsNullOrWhiteSpace(programFilesX86))
            {
                programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            }

            return Path.Combine(programFilesX86, AppName);
        }
    }

    internal static class Installer
    {
        public static void Install(InstallOptions options, Action<int, string>? progress)
        {
            CloseRunningPromptManager();
            Directory.CreateDirectory(options.InstallDirectory);

            progress?.Invoke(8, "Preparing application files...");
            var stagingDirectory = Path.Combine(Path.GetTempPath(), "PromptManagerSetup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);
            IReadOnlyList<string> installedFiles;
            var preserveRecoveryFiles = false;
            try
            {
                installedFiles = ExtractPayload(stagingDirectory, progress);
                if (!File.Exists(Path.Combine(stagingDirectory, ExeName))) throw new InvalidOperationException("The installer payload is incomplete.");
                InstallPayloadFiles(stagingDirectory, options.InstallDirectory, installedFiles);
            }
            catch (AggregateException) { preserveRecoveryFiles = true; throw; }
            finally
            {
                // Only the unique setup-owned staging directory is removed.
                if (!preserveRecoveryFiles && IsUnderInstallRoot(stagingDirectory, Path.GetTempPath())) Directory.Delete(stagingDirectory, true);
            }

            var appExe = Path.Combine(options.InstallDirectory, ExeName);
            if (!File.Exists(appExe))
            {
                throw new FileNotFoundException("The installed Prompt Manager executable was not found.", appExe);
            }

            progress?.Invoke(82, "Creating shortcuts...");
            if (options.CreateDesktopShortcut)
            {
                CreateShortcut(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"),
                    appExe,
                    options.InstallDirectory,
                    "Launch Prompt Manager");
            }

            if (options.CreateStartMenuShortcut)
            {
                var programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                CreateShortcut(
                    Path.Combine(programs, AppName + ".lnk"),
                    appExe,
                    options.InstallDirectory,
                    "Launch Prompt Manager");
            }

            IReadOnlyList<string> uninstallFiles = Array.Empty<string>();
            if (options.RegisterUninstallEntry)
            {
                progress?.Invoke(90, "Writing uninstall information...");
                uninstallFiles = WriteUninstallFiles(options.InstallDirectory);
                RegisterUninstallEntry(options.InstallDirectory, appExe);
            }

            WriteInstallManifest(options.InstallDirectory, installedFiles.Concat(uninstallFiles).Append(InstallManifestName));

            progress?.Invoke(96, "Finalizing installation...");
            if (options.LaunchAfterInstall)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = appExe,
                    WorkingDirectory = options.InstallDirectory,
                    UseShellExecute = true
                });
            }

            progress?.Invoke(100, "Installation complete.");
        }

        private static void CloseRunningPromptManager()
        {
            foreach (var process in Process.GetProcessesByName("PromptManager"))
            {
                using (process)
                    if (!process.HasExited) throw new InvalidOperationException("Save your changes and close Prompt Manager before installing. The installer will not force-close it.");
            }
        }

        internal static void InstallPayloadFiles(string stagingDirectory, string installDirectory, IReadOnlyList<string> files)
        {
            var previous = Path.Combine(installDirectory, InstallManifestName);
            var oldFiles = File.Exists(previous) ? ReadManifestEntries(previous).ToArray() : Array.Empty<string>();
            var affected = oldFiles.Concat(files).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var rollback = Path.Combine(stagingDirectory, ".rollback");
            Directory.CreateDirectory(rollback);
            var preserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in affected)
            {
                var target = GetManifestFilePath(installDirectory, file) ?? throw new InvalidOperationException("Invalid installation file path.");
                var backup = GetManifestFilePath(rollback, file) ?? throw new InvalidOperationException("Invalid rollback file path.");
                if (File.Exists(target)) { Directory.CreateDirectory(Path.GetDirectoryName(backup)!); File.Copy(target, backup, true); preserved.Add(file); }
            }
            var changed = new List<string>();
            try
            {
                foreach (var file in files)
                {
                    var source = GetManifestFilePath(stagingDirectory, file) ?? throw new InvalidOperationException("Invalid payload file path.");
                    var target = GetManifestFilePath(installDirectory, file)!;
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    var pending = target + ".setup-" + Guid.NewGuid().ToString("N") + ".tmp";
                    try { File.Copy(source, pending); File.Move(pending, target, true); changed.Add(file); }
                    finally { if (File.Exists(pending)) File.Delete(pending); }
                }
                foreach (var file in oldFiles.Except(files, StringComparer.OrdinalIgnoreCase))
                {
                    var target = GetManifestFilePath(installDirectory, file)!;
                    if (File.Exists(target)) { File.Delete(target); changed.Add(file); }
                }
            }
            catch (Exception installError)
            {
                try
                {
                    foreach (var file in changed.Distinct(StringComparer.OrdinalIgnoreCase).Reverse())
                    {
                        var target = GetManifestFilePath(installDirectory, file)!;
                        if (preserved.Contains(file)) File.Copy(GetManifestFilePath(rollback, file)!, target, true);
                        else if (File.Exists(target)) File.Delete(target);
                    }
                }
                catch (Exception rollbackError) { throw new AggregateException("Installation and recovery failed. Original files are retained in " + rollback, installError, rollbackError); }
                throw new InvalidOperationException("Installation failed. Previous application files were restored. " + installError.Message, installError);
            }
        }

        private static IReadOnlyList<string> ExtractPayload(string installDirectory, Action<int, string>? progress)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(PayloadResource)
                ?? throw new InvalidOperationException("The installer payload is missing.");
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var installRoot = GetInstallRoot(installDirectory);
            var installedFiles = new List<string>();
            var entries = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var destination = Path.GetFullPath(Path.Combine(installRoot, entry.FullName));
                if (!IsUnderInstallRoot(destination, installRoot))
                {
                    throw new InvalidOperationException("The installer payload contains an invalid file path.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, true);
                installedFiles.Add(ToManifestPath(entry.FullName));

                if (i % 12 == 0)
                {
                    var percent = 8 + (int)((i + 1) / (double)Math.Max(1, entries.Count) * 72);
                    progress?.Invoke(percent, "Extracting " + entry.Name);
                }
            }

            return installedFiles;
        }

        private static void RemovePreviousInstalledFiles(string installDirectory)
        {
            var manifestPath = Path.Combine(installDirectory, InstallManifestName);
            if (!File.Exists(manifestPath))
            {
                return;
            }

            var entries = ReadManifestEntries(manifestPath).ToList();
            foreach (var entry in entries.OrderByDescending(item => item.Length))
            {
                var filePath = GetManifestFilePath(installDirectory, entry);
                if (filePath is null || !File.Exists(filePath))
                {
                    continue;
                }

                File.Delete(filePath);
            }

            RemoveEmptyParentDirectories(installDirectory, entries);
        }

        private static void WriteInstallManifest(string installDirectory, IEnumerable<string> relativeFiles)
        {
            var manifestPath = Path.Combine(installDirectory, InstallManifestName);
            var entries = relativeFiles
                .Select(ToManifestPath)
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var lines = new List<string>
            {
                "# Prompt Manager install manifest",
                "# Generated " + DateTime.Now.ToString("O")
            };
            lines.AddRange(entries);
            File.WriteAllLines(manifestPath, lines);
        }

        private static IEnumerable<string> ReadManifestEntries(string manifestPath)
        {
            return File.ReadLines(manifestPath)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal));
        }

        private static string? GetManifestFilePath(string installDirectory, string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
            {
                return null;
            }

            var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(GetInstallRoot(installDirectory), normalizedRelativePath));
            return IsUnderInstallRoot(candidate, installDirectory) ? candidate : null;
        }

        private static void RemoveEmptyParentDirectories(string installDirectory, IEnumerable<string> relativeFiles)
        {
            var installRoot = GetInstallRoot(installDirectory);
            var directories = relativeFiles
                .Select(entry => GetManifestFilePath(installRoot, entry))
                .Where(path => path is not null)
                .Select(path => Path.GetDirectoryName(path!))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(path => path!.Length);

            foreach (var directory in directories)
            {
                var current = directory;
                while (!string.IsNullOrWhiteSpace(current)
                    && IsUnderInstallRoot(current, installRoot)
                    && !string.Equals(GetInstallRoot(current), installRoot, StringComparison.OrdinalIgnoreCase))
                {
                    if (!Directory.Exists(current) || Directory.EnumerateFileSystemEntries(current).Any())
                    {
                        break;
                    }

                    Directory.Delete(current);
                    current = Path.GetDirectoryName(current);
                }
            }
        }

        private static string GetInstallRoot(string installDirectory)
        {
            return Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsUnderInstallRoot(string path, string installDirectory)
        {
            var installRoot = GetInstallRoot(installDirectory);
            var rootPrefix = installRoot + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string ToManifestPath(string relativePath)
        {
            return relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/').TrimStart('/');
        }
        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("Windows Script Host is not available to create shortcuts.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.IconLocation = targetPath + ",0";
            shortcut.Description = description;
            shortcut.Save();
        }

        private static IReadOnlyList<string> WriteUninstallFiles(string installDirectory)
        {
            var uninstallScript = Path.Combine(installDirectory, UninstallScriptName);
            var uninstallCommand = Path.Combine(installDirectory, UninstallCommandName);

            var script = $$"""
$ErrorActionPreference = 'SilentlyContinue'
$installDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifest = Join-Path $installDirectory '{{InstallManifestName}}'
$root = [System.IO.Path]::GetFullPath($installDirectory).TrimEnd('\', '/')
$rootPrefix = $root + [System.IO.Path]::DirectorySeparatorChar
$removedPaths = New-Object System.Collections.Generic.List[string]

if (Get-Process PromptManager -ErrorAction SilentlyContinue) {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show('Save your changes and close Prompt Manager before uninstalling. Your prompt library will be kept.', 'Prompt Manager') | Out-Null
    exit 1
}
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
Remove-Item -LiteralPath (Join-Path $desktop 'Prompt Manager.lnk') -Force
Remove-Item -LiteralPath "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Prompt Manager.lnk" -Force
Remove-Item -LiteralPath "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PromptManager" -Recurse -Force

if (Test-Path -LiteralPath $manifest) {
    Get-Content -LiteralPath $manifest |
        Where-Object { $_ -and -not $_.StartsWith('#') } |
        Sort-Object Length -Descending |
        ForEach-Object {
            $relative = $_.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
            if (-not [System.IO.Path]::IsPathRooted($relative)) {
                $candidate = [System.IO.Path]::GetFullPath((Join-Path $installDirectory $relative))
                if ($candidate.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $candidate)) {
                    Remove-Item -LiteralPath $candidate -Force
                    $removedPaths.Add($candidate) | Out-Null
                }
            }
        }
}

$directories = $removedPaths |
    ForEach-Object { Split-Path -Parent $_ } |
    Sort-Object Length -Descending -Unique

foreach ($directory in $directories) {
    $current = $directory
    while ($current -and $current.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        if ((Test-Path -LiteralPath $current) -and -not (Get-ChildItem -LiteralPath $current -Force)) {
            Remove-Item -LiteralPath $current -Force
            $current = Split-Path -Parent $current
        }
        else {
            break
        }
    }
}

if ((Test-Path -LiteralPath $root) -and -not (Get-ChildItem -LiteralPath $root -Force)) {
    Remove-Item -LiteralPath $root -Force
}
""";

            File.WriteAllText(uninstallScript, script);
            File.WriteAllText(uninstallCommand, "@echo off" + Environment.NewLine + "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%~dp0" + UninstallScriptName + "\"");
            return new[]
            {
                ToManifestPath(UninstallScriptName),
                ToManifestPath(UninstallCommandName)
            };
        }
        private static void RegisterUninstallEntry(string installDirectory, string appExe)
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PromptManager");
            if (key is null)
            {
                return;
            }

            var uninstallCommand = Path.Combine(installDirectory, "Uninstall Prompt Manager.cmd");
            key.SetValue("DisplayName", AppName);
            key.SetValue("DisplayVersion", InstallerVersion);
            key.SetValue("Publisher", "Prompt Manager");
            key.SetValue("InstallLocation", installDirectory);
            key.SetValue("DisplayIcon", appExe);
            key.SetValue("UninstallString", "\"" + uninstallCommand + "\"");
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }

}

