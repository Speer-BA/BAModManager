using Microsoft.VisualBasic.FileIO;
using static BAModManager.Core.Loc;

namespace BAModManager.Core;

public static class PathUtil
{
    public const string DisabledSuffix = ".disabled";

    /// <summary>The path spelled exactly as on disk ("d:\steam\…" → "D:\Steam\…"), the way Steam passes it.</summary>
    public static string RealCase(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full)!.ToUpperInvariant();
            var result = root;
            foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = new DirectoryInfo(result).EnumerateFileSystemInfos()
                    .FirstOrDefault(i => string.Equals(i.Name, part, StringComparison.OrdinalIgnoreCase));
                if (match == null) return full;
                result = Path.Combine(result, match.Name);
            }
            return result;
        }
        catch
        {
            return path;
        }
    }

    /// <summary>Turns "a\b/../c" style paths into clean "a/b/c" and rejects anything that tries to escape.</summary>
    public static string NormalizeRel(string rel)
    {
        var parts = rel.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => p == ".." || p == "." || p.Contains(':')))
            throw new InvalidDataException(T("Unsafe path: {0}", rel));
        return string.Join('/', parts);
    }

    /// <summary>Full path for a game-relative path; guaranteed to stay inside the game folder.</summary>
    public static string ToFull(string gameDir, string rel)
    {
        var root = Path.GetFullPath(gameDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var clean = NormalizeRel(rel).Replace('/', Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, clean));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !string.Equals(full + Path.DirectorySeparatorChar, root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(T("Path leaves the game folder: {0}", rel));
        return full;
    }

    public static string ToRel(string gameDir, string full) =>
        Path.GetRelativePath(gameDir, full).Replace('\\', '/');

    public static string JoinRel(string? dir, string name)
    {
        var d = (dir ?? "").Replace('\\', '/').Trim('/');
        return d.Length == 0 ? name : d + "/" + name;
    }

    public static string FirstSegment(string rel)
    {
        var i = rel.IndexOf('/');
        return i < 0 ? rel : rel[..i];
    }

    public static string DirOf(string rel)
    {
        var i = rel.LastIndexOf('/');
        return i < 0 ? T("(game folder)") : rel[..i];
    }
}

/// <summary>Deletes go to the Recycle Bin where possible, so nothing is lost by accident.</summary>
public static class FileOps
{
    public static void DeleteFile(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }
        catch (Exception) when (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try
        {
            FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }
        catch (Exception) when (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    /// <summary>Removes folders left empty after a delete, stopping at protected folders.</summary>
    public static void RemoveEmptyParents(string gameDir, string fullPath, ISet<string> protectedRel)
    {
        var root = Path.GetFullPath(gameDir).TrimEnd(Path.DirectorySeparatorChar);
        var dir = Path.GetDirectoryName(fullPath);
        while (dir != null && dir.Length > root.Length && dir.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            var rel = PathUtil.ToRel(gameDir, dir);
            if (protectedRel.Contains(rel)) break;
            try
            {
                if (Directory.EnumerateFileSystemEntries(dir).Any()) break;
                Directory.Delete(dir);
            }
            catch
            {
                break;
            }
            dir = Path.GetDirectoryName(dir);
        }
    }
}
