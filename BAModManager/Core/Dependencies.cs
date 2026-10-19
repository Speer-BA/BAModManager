using static BAModManager.Core.Loc;
namespace BAModManager.Core;

public enum DepProblem { Missing, Outdated, Disabled, Incompatible }

public sealed record DepIssue(ModEntry Mod, DepProblem Problem, DependencyRef Ref, ModEntry? Found)
{
    /// <summary>Friendly name of the other mod.</summary>
    public string OtherName => Found?.Name ?? Ref.Id;

    public string Describe() => Problem switch
    {
        DepProblem.Missing => T("{0} - not installed", OtherName + Suffix(Ref.Version)),
        DepProblem.Outdated => T("{0} - you have {1}", OtherName + Suffix(Ref.Version), Found?.Version ?? "?"),
        DepProblem.Disabled => T("{0} - installed but switched off", OtherName),
        DepProblem.Incompatible => T("{0} - doesn't work together with {1}", OtherName, Mod.Name),
        _ => OtherName,
    };

    private static string Suffix(string? rule) => ModVersion.Describe(rule) is { Length: > 0 } d ? $" ({d})" : "";
}

/// <summary>Works out which mods need which, using the ids in mod.json.</summary>
public static class Dependencies
{
    /// <summary>
    /// The installed mod with this id. Mods copied in by hand have no id, so a .dll whose file name matches
    /// the id (or its last part: "author.coolmod" → CoolMod.dll) counts too.
    /// </summary>
    public static ModEntry? FindProvider(IEnumerable<ModEntry> all, string id)
    {
        var list = all as IList<ModEntry> ?? all.ToList();
        var exact = list.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;

        var shortId = id.Contains('.') ? id[(id.LastIndexOf('.') + 1)..] : id;
        return list.FirstOrDefault(e =>
        {
            if (e.Id != null) return false;
            var files = e.Package != null ? e.Package.Files : new List<string> { ModService.StripDisabled(e.LooseRel ?? "") };
            return files.Any(f =>
                Path.GetExtension(f).Equals(".dll", StringComparison.OrdinalIgnoreCase) &&
                (Path.GetFileNameWithoutExtension(f).Equals(shortId, StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFileNameWithoutExtension(f).Equals(id, StringComparison.OrdinalIgnoreCase)));
        });
    }

    /// <summary>Problems with one mod's requirements and incompatibilities.</summary>
    public static List<DepIssue> Check(ModEntry mod, IReadOnlyList<ModEntry> all)
    {
        var issues = new List<DepIssue>();
        var info = mod.Info;
        if (info == null) return issues;

        foreach (var req in info.Requires)
        {
            var found = FindProvider(all, req.Id);
            if (found == null) issues.Add(new DepIssue(mod, DepProblem.Missing, req, null));
            else if (!ModVersion.Satisfies(found.Version, req.Version)) issues.Add(new DepIssue(mod, DepProblem.Outdated, req, found));
            else if (!found.Enabled) issues.Add(new DepIssue(mod, DepProblem.Disabled, req, found));
        }
        foreach (var bad in info.Incompatible)
        {
            var found = FindProvider(all, bad.Id);
            if (found != null && found != mod && found.Enabled && ModVersion.Satisfies(found.Version, bad.Version))
                issues.Add(new DepIssue(mod, DepProblem.Incompatible, bad, found));
        }
        return issues;
    }

    /// <summary>Mods whose mod.json requires <paramref name="mod"/>.</summary>
    public static List<ModEntry> RequiredBy(ModEntry mod, IReadOnlyList<ModEntry> all) =>
        all.Where(other => other != mod && other.Info != null &&
                           other.Info.Requires.Any(r => FindProvider(all, r.Id) == mod))
           .ToList();

    /// <summary>Everything <paramref name="mods"/> need, directly or indirectly, that is installed.</summary>
    public static List<ModEntry> RequiredTree(IEnumerable<ModEntry> mods, IReadOnlyList<ModEntry> all)
    {
        var result = new List<ModEntry>();
        var queue = new Queue<ModEntry>(mods);
        var seen = new HashSet<ModEntry>(queue);
        while (queue.Count > 0)
        {
            var m = queue.Dequeue();
            foreach (var req in m.Info?.Requires ?? new List<DependencyRef>())
            {
                var found = FindProvider(all, req.Id);
                if (found != null && seen.Add(found))
                {
                    result.Add(found);
                    queue.Enqueue(found);
                }
            }
        }
        return result;
    }

    /// <summary>Everything that needs <paramref name="mods"/>, directly or indirectly.</summary>
    public static List<ModEntry> DependentTree(IEnumerable<ModEntry> mods, IReadOnlyList<ModEntry> all)
    {
        var result = new List<ModEntry>();
        var queue = new Queue<ModEntry>(mods);
        var seen = new HashSet<ModEntry>(queue);
        while (queue.Count > 0)
        {
            var m = queue.Dequeue();
            foreach (var dep in RequiredBy(m, all))
            {
                if (seen.Add(dep))
                {
                    result.Add(dep);
                    queue.Enqueue(dep);
                }
            }
        }
        return result;
    }
}
