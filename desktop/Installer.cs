using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ObsidianSetup
{
    public static class InstallerCore
    {
        public static void InstallPayload(Stream payload, string target)
        {
            target = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
            if (Directory.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("설치 폴더가 다른 위치에 연결되어 있습니다.");
            string stage = target + ".stage-" + Guid.NewGuid().ToString("N");
            string backup = Path.Combine(target, "backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            List<string> written = new List<string>();
            try
            {
                Directory.CreateDirectory(stage);
                using (ZipArchive zip = new ZipArchive(payload, ZipArchiveMode.Read, true))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string name = entry.FullName.Replace('\\', '/');
                        string top = name.Split('/')[0].ToLowerInvariant();
                        if (name.StartsWith("/") || name.IndexOf(':') >= 0 || Array.IndexOf(name.Split('/'), "..") >= 0 ||
                            top == "settings.json" || top == "key.bin" || top == "runtime-key.dpapi" ||
                            top == "data" || top == ".tunnel" || top == "backups")
                            throw new InvalidDataException("설치 파일에 허용되지 않은 경로 또는 사용자 설정이 포함되어 있습니다.");
                        string destination = Path.GetFullPath(Path.Combine(stage, name.Replace('/', Path.DirectorySeparatorChar)));
                        if (!destination.StartsWith(stage + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("설치 파일 경로가 잘못되었습니다.");
                        if (name.EndsWith("/")) { Directory.CreateDirectory(destination); continue; }
                        Directory.CreateDirectory(Path.GetDirectoryName(destination));
                        using (Stream input = entry.Open())
                        using (Stream output = File.Create(destination)) input.CopyTo(output);
                    }
                }
                // Validate all destinations before replacing an existing installation.
                string[] files = Directory.GetFiles(stage, "*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string relative = file.Substring(stage.Length + 1);
                    string destination = Path.Combine(target, relative);
                    for (string cursor = destination; cursor != null && cursor.Length >= target.Length; cursor = Path.GetDirectoryName(cursor))
                        if ((File.Exists(cursor) || Directory.Exists(cursor)) && (File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                            throw new InvalidDataException("설치 대상에 외부 연결 경로가 있습니다.");
                }
                Directory.CreateDirectory(target);
                foreach (string file in files)
                {
                    string relative = file.Substring(stage.Length + 1);
                    string destination = Path.Combine(target, relative);
                    if (File.Exists(destination))
                    {
                        string saved = Path.Combine(backup, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(saved));
                        File.Copy(destination, saved, false);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    written.Add(relative);
                    File.Copy(file, destination, true);
                }
            }
            catch
            {
                for (int i = written.Count - 1; i >= 0; i--)
                {
                    string destination = Path.Combine(target, written[i]);
                    string saved = Path.Combine(backup, written[i]);
                    if (File.Exists(saved)) File.Copy(saved, destination, true);
                    else if (File.Exists(destination)) File.Delete(destination);
                }
                throw;
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }
    }

    static class Program
    {
        public static readonly string Target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ObsidianChatGPT");
        static readonly string Shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Obsidian ChatGPT.lnk");
        const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ObsidianChatGPT";

        static void StopOwnedTasks(bool remove)
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            service.Connect(); dynamic folder = service.GetFolder("\\");
            foreach (string name in new string[] { "Obsidian ChatGPT", "Obsidian DigitalBrain Tunnel" })
            {
                dynamic task;
                try { task = folder.GetTask(name); }
                catch (FileNotFoundException e) { if (e.HResult == unchecked((int)0x80070002)) continue; throw; }
                catch (COMException e) { if (e.ErrorCode == unchecked((int)0x80070002)) continue; throw; }
                bool owned = false;
                foreach (dynamic action in task.Definition.Actions)
                {
                    string path = Convert.ToString(action.Path);
                    string args = Convert.ToString(action.Arguments);
                    owned |= String.Equals(path, Path.Combine(Target, "ObsidianChatGPT.exe"), StringComparison.OrdinalIgnoreCase) ||
                        args.IndexOf("\"" + Path.Combine(Target, "Start-Tunnel.ps1") + "\"", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                if (!owned) throw new InvalidOperationException("같은 이름의 다른 예약 작업이 있습니다. 기존 작업을 확인하세요.");
                task.Stop(0);
                if (remove) folder.DeleteTask(name, 0);
                else task.Enabled = false;
            }
            foreach (Process process in Process.GetProcessesByName("ObsidianChatGPT"))
                using (process)
                {
                    if (process.HasExited) continue;
                    if (!String.Equals(process.MainModule.FileName, Path.Combine(Target, "ObsidianChatGPT.exe"), StringComparison.OrdinalIgnoreCase)) continue;
                    if (process.MainWindowHandle != IntPtr.Zero) process.CloseMainWindow();
                    if (!process.WaitForExit(5000)) throw new InvalidOperationException("설정 창을 닫은 후 다시 실행하세요.");
                }
        }

        public static void Install()
        {
            if (Directory.Exists(Target) && !File.Exists(Path.Combine(Target, "ObsidianChatGPT.exe")) && !File.Exists(Path.Combine(Target, "Start-Tunnel.ps1")))
                throw new InvalidOperationException("설치 위치에 다른 폴더가 있습니다: " + Target);
            StopOwnedTasks(false);
            using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload"))
            {
                if (payload == null) throw new InvalidDataException("설치 데이터를 찾지 못했습니다.");
                InstallerCore.InstallPayload(payload, Target);
            }
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            dynamic shortcut = shell.CreateShortcut(Shortcut);
            shortcut.TargetPath = Path.Combine(Target, "ObsidianChatGPT.exe");
            shortcut.WorkingDirectory = Target;
            shortcut.Description = "옵시디언 노트를 ChatGPT에 읽기 전용으로 연결";
            shortcut.Save();
            using (RegistryKey registry = Registry.CurrentUser.CreateSubKey(RegistryPath))
            {
                registry.SetValue("DisplayName", "Obsidian ChatGPT 연결");
                registry.SetValue("DisplayVersion", "0.2.0");
                registry.SetValue("InstallLocation", Target);
                registry.SetValue("DisplayIcon", Path.Combine(Target, "ObsidianChatGPT.exe"));
                registry.SetValue("UninstallString", "\"" + Path.Combine(Target, "Uninstall.exe") + "\" --uninstall");
                registry.SetValue("NoModify", 1, RegistryValueKind.DWord);
                registry.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        static void Uninstall()
        {
            if (MessageBox.Show("연결 프로그램과 저장된 설정·암호화된 키를 제거할까요?\n옵시디언 노트는 삭제하지 않습니다.", "Obsidian ChatGPT 제거", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            string temporary = Path.Combine(Path.GetTempPath(), "ObsidianChatGPT-remove-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(Application.ExecutablePath, temporary);
            Process.Start(new ProcessStartInfo(temporary, "--remove") { UseShellExecute = false, CreateNoWindow = true });
        }

        static void RemoveInstallation()
        {
            StopOwnedTasks(true);
            if (Directory.Exists(Target))
            {
                if ((File.GetAttributes(Target) & FileAttributes.ReparsePoint) != 0 || !File.Exists(Path.Combine(Target, "ObsidianChatGPT.exe")))
                    throw new InvalidOperationException("설치 경로를 확인할 수 없어 제거를 중지했습니다.");
                // The original uninstaller is exiting from the installation directory.
                for (int attempt = 0; ; attempt++)
                {
                    try { Directory.Delete(Target, true); break; }
                    catch (IOException) { if (attempt >= 9) throw; Thread.Sleep(500); }
                }
            }
            if (File.Exists(Shortcut)) File.Delete(Shortcut);
            Registry.CurrentUser.DeleteSubKeyTree(RegistryPath, false);
            MessageBox.Show("프로그램을 제거했습니다. 옵시디언 노트는 그대로 유지됩니다.", "제거 완료");
        }

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                if (args.Length == 1 && args[0] == "--remove") { RemoveInstallation(); return; }
                if ((args.Length == 1 && args[0] == "--uninstall") || String.Equals(Path.GetFileName(Application.ExecutablePath), "Uninstall.exe", StringComparison.OrdinalIgnoreCase)) { Uninstall(); return; }
                Application.Run(new SetupForm());
            }
            catch (Exception e) { MessageBox.Show(e.Message, "작업을 완료하지 못했습니다", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    class SetupForm : Form
    {
        readonly Button install = new Button();
        readonly Label status = new Label();
        readonly ProgressBar progress = new ProgressBar();
        bool installing;
        public SetupForm()
        {
            Text = "Obsidian ChatGPT 설치"; ClientSize = new Size(540, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("맑은 고딕", 10F);
            BackColor = Color.FromArgb(249, 250, 252); AutoScaleMode = AutoScaleMode.Dpi;
            Controls.Add(new Label { Text = "내 노트와 대화하는 가장 간단한 시작", Font = new Font(Font.FontFamily, 17F, FontStyle.Bold), Bounds = new Rectangle(25, 25, 495, 45) });
            Controls.Add(new Label { Text = "설치 후 보관함 폴더, Tunnel ID, API 키를 입력하세요.\nPython이나 WSL을 따로 설치할 필요가 없습니다.", Bounds = new Rectangle(28, 85, 480, 60) });
            Controls.Add(new Label { Text = "현재 Windows 사용자에게 설치됩니다.\n기존 설정과 암호화된 키는 유지합니다.", ForeColor = Color.DimGray, Bounds = new Rectangle(28, 155, 480, 50) });
            install.Text = "설치하고 열기"; install.Bounds = new Rectangle(28, 222, 180, 43);
            install.BackColor = Color.FromArgb(76, 67, 194); install.ForeColor = Color.White; install.FlatStyle = FlatStyle.Flat;
            Controls.Add(install);
            progress.Bounds = new Rectangle(225, 232, 285, 20); progress.Visible = false; progress.Style = ProgressBarStyle.Marquee; Controls.Add(progress);
            status.Bounds = new Rectangle(28, 280, 490, 45); Controls.Add(status);
            install.Click += async delegate
            {
                installing = true; install.Enabled = false; progress.Visible = true; status.Text = "설치 중입니다…";
                try
                {
                    await Task.Run((Action)Program.Install);
                    installing = false;
                    Process.Start(new ProcessStartInfo(Path.Combine(Program.Target, "ObsidianChatGPT.exe")) { UseShellExecute = true });
                    Close();
                }
                catch (Exception e) { status.Text = "설치 실패: " + e.Message; installing = false; install.Enabled = true; progress.Visible = false; }
            };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (installing) e.Cancel = true; };
        }
    }
}
