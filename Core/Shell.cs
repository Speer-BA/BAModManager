using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BAModManager.Core;

/// <summary>
/// Makes .bamod files open with the Mod Manager when double-clicked.
/// Per-user registration (HKCU), so no administrator rights are needed.
/// </summary>
public static class FileAssociation
{
    public const string Extension = ".bamod";
    private const string ProgId = AppInfo.Id + ".Package";

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    private static string? ExePath => Environment.ProcessPath;

    private static string Command(string exe) => $"\"{exe}\" \"%1\"";

    /// <summary>True when .bamod files open with this copy of the Mod Manager.</summary>
    public static bool IsRegisteredHere()
    {
        try
        {
            var exe = ExePath;
            if (exe == null) return false;
            using var ext = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{Extension}");
            using var cmd = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ProgId}\shell\open\command");
            return ext?.GetValue("") as string == ProgId &&
                   string.Equals(cmd?.GetValue("") as string, Command(exe), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Points .bamod files at this exe. Does nothing if that's already the case.</summary>
    public static bool Register()
    {
        try
        {
            var exe = ExePath;
            if (exe == null || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
            if (IsRegisteredHere()) return true;

            using (var ext = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Extension}"))
            {
                ext.SetValue("", ProgId);
                ext.SetValue("Content Type", "application/zip");
            }
            using (var prog = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}"))
            {
                prog.SetValue("", "Broken Arrow mod package");
                using (var icon = prog.CreateSubKey("DefaultIcon")) icon.SetValue("", $"\"{exe}\",0");
                using (var cmd = prog.CreateSubKey(@"shell\open\command")) cmd.SetValue("", Command(exe));
            }
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Unregister()
    {
        try
        {
            using (var ext = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{Extension}"))
            {
                if (ext?.GetValue("") as string != ProgId) return; // belongs to something else
            }
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{Extension}", false);
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", false);
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // ignore
        }
    }
}

/// <summary>
/// Keeps one Mod Manager window: a second launch (e.g. double-clicking a .bamod)
/// hands its files to the running window and exits.
/// </summary>
public static class SingleInstance
{
    private static string PipeName => AppInfo.Id + "-" + Environment.UserName;

    /// <summary>Sends files to an already running Mod Manager. False if none answered.</summary>
    public static bool SendToRunning(IEnumerable<string> files)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(2000);
            using var writer = new StreamWriter(client);
            writer.WriteLine("BAMM1");
            foreach (var f in files) writer.WriteLine(Path.GetFullPath(f));
            writer.WriteLine();
            writer.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Action<string[]>? _onFiles;
    private static bool _serverStarted;

    /// <summary>
    /// Listens for files from later launches. <paramref name="onFiles"/> runs on a background thread.
    /// Calling it again only swaps the receiver (the launcher and the mod manager window take turns).
    /// </summary>
    public static void StartServer(Action<string[]> onFiles)
    {
        _onFiles = onFiles;
        if (_serverStarted) return;
        _serverStarted = true;
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1);
                    server.WaitForConnection();
                    using var reader = new StreamReader(server);
                    if (reader.ReadLine() != "BAMM1") continue;
                    var files = new List<string>();
                    string? line;
                    while ((line = reader.ReadLine()) != null && line.Length > 0 && files.Count < 100)
                        files.Add(line);
                    _onFiles?.Invoke(files.ToArray());
                }
                catch
                {
                    Thread.Sleep(500);
                }
            }
        })
        {
            IsBackground = true,
            Name = "BAModManager single instance",
        };
        thread.Start();
    }
}

/// <summary>
/// Uninstall leftovers. A program can't delete its own running exe, and the usual trick (a hidden
/// "cmd /c ping … & del" started on exit) is exactly what antivirus heuristics flag as malware -
/// so we remove the files next to the exe and show the exe in Explorer for the user to delete.
/// </summary>
public static class SelfDelete
{
    /// <summary>Deletes the files next to the exe now; returns the exe's path (null if not a normal exe).</summary>
    public static string? Schedule(params string[] alsoDelete)
    {
        var exe = Environment.ProcessPath;
        if (exe == null || !exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(exe).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return null;

        var folder = Path.GetDirectoryName(exe)!;
        foreach (var name in alsoDelete.Prepend("Inter-LICENSE.txt"))
        {
            try
            {
                var file = Path.Combine(folder, name);
                if (File.Exists(file)) File.Delete(file);
            }
            catch { /* the user can delete it with the exe */ }
        }
        return exe;
    }

    /// <summary>Opens Explorer with the exe selected, so the user can delete it.</summary>
    public static void ShowExe(string? exe)
    {
        if (exe == null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{exe}\"") { UseShellExecute = true })?.Dispose();
        }
        catch { /* fine */ }
    }
}
