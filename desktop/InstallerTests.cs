using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using ObsidianSetup;

class InstallerTests
{
    static MemoryStream Archive(params string[] pairs)
    {
        MemoryStream stream = new MemoryStream();
        using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
            for (int i = 0; i < pairs.Length; i += 2)
                using (StreamWriter writer = new StreamWriter(zip.CreateEntry(pairs[i]).Open(), new UTF8Encoding(false)))
                    writer.Write(pairs[i + 1]);
        stream.Position = 0;
        return stream;
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void TestUpdatePreservesUserSettings(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "ObsidianChatGPT.exe"), "old app");
        File.WriteAllText(Path.Combine(root, "settings.json"), "user settings");
        File.WriteAllBytes(Path.Combine(root, "key.bin"), new byte[] { 1, 2, 3 });
        using (MemoryStream zip = Archive("ObsidianChatGPT.exe", "new app", "server.py", "new server"))
            InstallerCore.InstallPayload(zip, root);
        Assert(File.ReadAllText(Path.Combine(root, "ObsidianChatGPT.exe")) == "new app", "Upgrade did not deploy new application");
        Assert(File.ReadAllText(Path.Combine(root, "server.py")) == "new server", "Server was not installed");
        Assert(File.ReadAllText(Path.Combine(root, "settings.json")) == "user settings", "Existing user settings changed");
        Assert(File.ReadAllBytes(Path.Combine(root, "key.bin"))[0] == 1, "Existing encrypted key changed");
    }

    static void TestRejectsEscapeBeforeChangingInstall(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "server.py"), "original");
        bool rejected = false;
        using (MemoryStream zip = Archive("server.py", "replacement", "../escape.txt", "outside"))
        {
            try { InstallerCore.InstallPayload(zip, root); }
            catch (InvalidDataException) { rejected = true; }
        }
        Assert(rejected, "Path traversal archive was accepted");
        Assert(File.ReadAllText(Path.Combine(root, "server.py")) == "original", "Rejected archive changed existing install");
        Assert(!File.Exists(Path.Combine(Path.GetDirectoryName(root), "escape.txt")), "Archive escaped install directory");
    }

    static void TestRejectsBundledSecrets(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "settings.json"), "recipient settings");
        bool rejected = false;
        using (MemoryStream zip = Archive("settings.json", "developer settings"))
        {
            try { InstallerCore.InstallPayload(zip, root); }
            catch (InvalidDataException) { rejected = true; }
        }
        Assert(rejected, "Payload may overwrite recipient settings");
        Assert(File.ReadAllText(Path.Combine(root, "settings.json")) == "recipient settings", "Recipient settings were overwritten");
    }

    public static int Main()
    {
        string temp = Path.Combine(Path.GetTempPath(), "obsidian-installer-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        int failed = 0;
        Action<string>[] tests = { TestUpdatePreservesUserSettings, TestRejectsEscapeBeforeChangingInstall, TestRejectsBundledSecrets };
        try
        {
            for (int i = 0; i < tests.Length; i++)
            {
                try { tests[i](Path.Combine(temp, i.ToString())); Console.WriteLine("PASS " + tests[i].Method.Name); }
                catch (Exception e) { Console.WriteLine("FAIL " + tests[i].Method.Name + ": " + e.Message); failed++; }
            }
        }
        finally { Directory.Delete(temp, true); }
        return failed == 0 ? 0 : 1;
    }
}
