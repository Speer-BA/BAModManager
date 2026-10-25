using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BAModManager.Core;

public static class GameLocator
{
    /// <summary>A Unity game folder has a "Something_Data" folder (and usually UnityPlayer.dll).</summary>
    public static bool IsGameFolder(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return false;
        try
        {
            return File.Exists(Path.Combine(dir, "UnityPlayer.dll"))
                   || Directory.EnumerateDirectories(dir, "*_Data").Any();
        }
        catch
        {
            return false;
        }
    }

    public static string? FindExe(string dir)
    {
        try
        {
            foreach (var dataDir in Directory.EnumerateDirectories(dir, "*_Data"))
            {
                var name = Path.GetFileName(dataDir);
                var exe = Path.Combine(dir, name[..^"_Data".Length] + ".exe");
                if (File.Exists(exe)) return exe;
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

    public static bool IsGameRunning(string dir)
    {
        var exe = FindExe(dir);
        if (exe == null) return false;
        var procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe));
        var running = procs.Length > 0;
        foreach (var p in procs) p.Dispose();
        return running;
    }

    /// <summary>Steam app id from steamapps\appmanifest_*.acf, so Play can launch through Steam.</summary>
    public static string? FindSteamAppId(string gameDir)
    {
        try
        {
            var dir = new DirectoryInfo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDir)));
            var common = dir.Parent;
            var steamapps = common?.Parent;
            if (common == null || steamapps == null || !common.Name.Equals("common", StringComparison.OrdinalIgnoreCase))
                return null;

            foreach (var acf in Directory.EnumerateFiles(steamapps.FullName, "appmanifest_*.acf"))
            {
                var text = File.ReadAllText(acf);
                var install = Regex.Match(text, "\"installdir\"\\s+\"([^\"]+)\"");
                if (!install.Success || !install.Groups[1].Value.Equals(dir.Name, StringComparison.OrdinalIgnoreCase)) continue;
                var id = Regex.Match(text, "\"appid\"\\s+\"(\\d+)\"");
                if (id.Success) return id.Groups[1].Value;
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

    public static string? AutoDetect(string steamFolderName)
    {
        foreach (var lib in SteamLibraries())
        {
            var candidate = Path.Combine(lib, "steamapps", "common", steamFolderName);
            if (IsGameFolder(candidate)) return candidate;
        }
        return null;
    }

    private static IEnumerable<string> SteamLibraries()
    {
        var steamRoots = new List<string>();

        void FromRegistry(RegistryKey hive, string subKey, string valueName)
        {
            try
            {
                using var key = hive.OpenSubKey(subKey);
                if (key?.GetValue(valueName) is string s && s.Length > 0)
                    steamRoots.Add(s.Replace('/', '\\'));
            }
            catch
            {
                // ignore
            }
        }

        FromRegistry(Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        FromRegistry(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        FromRegistry(Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");

        var libraries = new List<string>(steamRoots);

        // Every extra Steam library is listed in libraryfolders.vdf
        foreach (var root in steamRoots)
        {
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            try
            {
                if (!File.Exists(vdf)) continue;
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            }
            catch
            {
                // ignore
            }
        }

        // Common manual locations, in case Steam isn't registered
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                var r = drive.RootDirectory.FullName;
                libraries.Add(Path.Combine(r, "Steam"));
                libraries.Add(Path.Combine(r, "SteamLibrary"));
                libraries.Add(Path.Combine(r, "Games", "Steam"));
                libraries.Add(Path.Combine(r, "Program Files (x86)", "Steam"));
            }
            catch
            {
                // ignore
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lib in libraries)
        {
            string full;
            try { full = Path.GetFullPath(lib); }
            catch { continue; }
            if (seen.Add(full)) yield return full;
        }
    }
}
