using System.Text.Json;
using System.Text.Json.Serialization;
using static BAModManager.Core.Loc;

namespace BAModManager.Core;

/// <summary>A requirement / optional / incompatible entry in mod.json.</summary>
public sealed class DependencyRef
{
    public string Id { get; set; } = "";

    /// <summary>"*" (any), "1.2" (that version or newer), ">=1.2", "<2.0", ">=1.2, <2.0", "^1.2", "~1.2", "=1.2.3".</summary>
    public string Version { get; set; } = "*";

    /// <summary>Where players can download it (optional).</summary>
    public string? Url { get; set; }
}

/// <summary>
/// Contents of mod.json inside a .bamod package. Only id, name and version are required.
/// </summary>
public sealed class ModInfo
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    public string? GameVersion { get; set; }
    public string? Website { get; set; }

    /// <summary>"modpack" for a modpack; empty for a normal mod.</summary>
    public string? Type { get; set; }

    /// <summary>GitHub "owner/repo" whose latest release (.bamod) is this package's newest version.</summary>
    public string? Repo { get; set; }

    [JsonIgnore]
    public bool IsModpack => string.Equals(Type, "modpack", StringComparison.OrdinalIgnoreCase);

    public List<DependencyRef> Requires { get; set; } = new();
    public List<DependencyRef> Optional { get; set; } = new();
    public List<DependencyRef> Incompatible { get; set; } = new();

    public const string FileName = "mod.json";

    /// <summary>
    /// Reads mod.json leniently. Dependencies may be written as
    /// { "id": "&gt;=1.2" }, { "id": { "version": "&gt;=1.2", "url": "…" } } or [ "id &gt;=1.2", { "id": "…" } ].
    /// </summary>
    public static ModInfo Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(Loc.T("mod.json must contain a { … } object."));

        var info = new ModInfo();
        foreach (var prop in root.EnumerateObject())
        {
            switch (prop.Name.ToLowerInvariant())
            {
                case "id": info.Id = Str(prop.Value); break;
                case "name": info.Name = Str(prop.Value); break;
                case "version": info.Version = Str(prop.Value); break;
                case "type": info.Type = Str(prop.Value); break;
                case "repo":
                case "updates":
                    info.Repo = GitHubRepo.Parse(Str(prop.Value)); break;
                case "author":
                case "authors":
                    info.Author = prop.Value.ValueKind == JsonValueKind.Array
                        ? string.Join(", ", prop.Value.EnumerateArray().Select(Str).Where(s => !string.IsNullOrWhiteSpace(s)))
                        : Str(prop.Value);
                    break;
                case "description": info.Description = Str(prop.Value); break;
                case "gameversion": info.GameVersion = Str(prop.Value); break;
                case "website":
                case "url": info.Website = Str(prop.Value); break;
                case "requires":
                case "dependencies": info.Requires = ParseDeps(prop.Value); break;
                case "optional": info.Optional = ParseDeps(prop.Value); break;
                case "incompatible":
                case "conflicts": info.Incompatible = ParseDeps(prop.Value); break;
            }
        }

        if (string.IsNullOrWhiteSpace(info.Id)) throw new InvalidDataException(Loc.T("mod.json has no \"id\"."));
        if (string.IsNullOrWhiteSpace(info.Name)) info.Name = info.Id;
        return info;
    }

    private static string? Str(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString()?.Trim(),
        JsonValueKind.Number => e.GetRawText(),
        _ => null,
    };

    private static List<DependencyRef> ParseDeps(JsonElement e)
    {
        var list = new List<DependencyRef>();
        if (e.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in e.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Object)
                {
                    var d = DepFromObject(p.Value);
                    d.Id = p.Name;
                    list.Add(d);
                }
                else
                {
                    list.Add(new DependencyRef { Id = p.Name, Version = Str(p.Value) ?? "*" });
                }
            }
        }
        else if (e.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in e.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object) list.Add(DepFromObject(item));
                else if (Str(item) is { Length: > 0 } s) list.Add(ParseDepLine(s));
            }
        }
        list.RemoveAll(d => string.IsNullOrWhiteSpace(d.Id));
        return list;
    }

    private static DependencyRef DepFromObject(JsonElement o)
    {
        var d = new DependencyRef();
        foreach (var p in o.EnumerateObject())
        {
            switch (p.Name.ToLowerInvariant())
            {
                case "id": d.Id = Str(p.Value) ?? ""; break;
                case "version": d.Version = Str(p.Value) ?? "*"; break;
                case "url":
                case "website": d.Url = Str(p.Value); break;
            }
        }
        return d;
    }

    /// <summary>"author.coolmod &gt;=1.2" → id + version rule. Used by mod.json arrays and the package tool.</summary>
    public static DependencyRef ParseDepLine(string line)
    {
        line = line.Trim();
        int split = line.IndexOfAny(new[] { ' ', '\t', '>', '<', '=', '^', '~' });
        if (split < 0) return new DependencyRef { Id = line };
        var range = line[split..].Trim();
        return new DependencyRef { Id = line[..split].Trim(), Version = range.Length == 0 ? "*" : range };
    }

    /// <summary>mod.json text, as written by the Create package tool.</summary>
    public string ToJson()
    {
        static object? Deps(List<DependencyRef> deps) => deps.Count == 0
            ? null
            : deps.ToDictionary(d => d.Id, d => d.Url == null ? (object)d.Version : new { version = d.Version, url = d.Url });

        var obj = new Dictionary<string, object?>
        {
            ["id"] = Id,
            ["name"] = Name,
            ["version"] = Version,
            ["type"] = Type,
            ["repo"] = Repo,
            ["author"] = Author,
            ["description"] = Description,
            ["gameVersion"] = GameVersion,
            ["website"] = Website,
            ["requires"] = Deps(Requires),
            ["optional"] = Deps(Optional),
            ["incompatible"] = Deps(Incompatible),
        };
        foreach (var k in obj.Where(kv => kv.Value is null or "").Select(kv => kv.Key).ToList()) obj.Remove(k);

        return JsonSerializer.Serialize(obj, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
    }
}

/// <summary>Loose version numbers ("1.2", "1.2.3-beta") and simple version rules.</summary>
public static class ModVersion
{
    public static int Compare(string a, string b)
    {
        static string[] Parts(string v) => v.Trim().TrimStart('v', 'V').Split('+')[0].Split('.', '-');
        var pa = Parts(a);
        var pb = Parts(b);
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var x = i < pa.Length ? pa[i] : "0";
            var y = i < pb.Length ? pb[i] : "0";
            int c = int.TryParse(x, out var nx) && int.TryParse(y, out var ny)
                ? nx.CompareTo(ny)
                : string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
        }
        return 0;
    }

    /// <summary>Does <paramref name="version"/> meet <paramref name="rule"/>? Unknown versions count as OK.</summary>
    public static bool Satisfies(string? version, string? rule)
    {
        if (string.IsNullOrWhiteSpace(rule) || rule.Trim() == "*") return true;
        if (string.IsNullOrWhiteSpace(version)) return true;

        foreach (var raw in rule.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var clause = raw.Trim();
            string op = new(clause.TakeWhile(ch => "<>=^~!".Contains(ch)).ToArray());
            var v = clause[op.Length..].Trim();
            if (v.Length == 0) continue;
            int c = Compare(version, v);
            bool ok = op switch
            {
                ">=" or "" => c >= 0,          // a bare number means "this version or newer"
                ">" => c > 0,
                "<=" => c <= 0,
                "<" => c < 0,
                "=" or "==" => c == 0,
                "!=" => c != 0,
                "^" => c >= 0 && Compare(version, Bump(v, 0)) < 0,
                "~" => c >= 0 && Compare(version, Bump(v, 1)) < 0,
                _ => true,
            };
            if (!ok) return false;
        }
        return true;
    }

    private static string Bump(string v, int index)
    {
        var parts = v.TrimStart('v', 'V').Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToList();
        while (parts.Count <= index) parts.Add(0);
        parts[index]++;
        return string.Join('.', parts.Take(index + 1));
    }

    /// <summary>Human text for a rule: "any version", "1.2 or newer", "&gt;=1.2, &lt;2.0".</summary>
    public static string Describe(string? rule)
    {
        if (string.IsNullOrWhiteSpace(rule) || rule.Trim() == "*") return "";
        var r = rule.Trim();
        if (char.IsDigit(r[0])) return T("{0} or newer", r);
        if (r.StartsWith(">=") && !r.Contains(',') && !r[2..].Trim().Contains(' ')) return T("{0} or newer", r[2..].Trim());
        return r;
    }
}

/// <summary>Turns "https://github.com/owner/repo/…", "github.com/owner/repo" or "owner/repo" into "owner/repo".</summary>
public static class GitHubRepo
{
    private static readonly System.Text.RegularExpressions.Regex Pattern =
        new(@"^(?:https?://)?(?:www\.)?(?:github\.com/)?([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+?)(?:\.git)?(?:[/?#].*)?$");

    public static string? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = Pattern.Match(text.Trim());
        if (!m.Success) return null;
        var owner = m.Groups[1].Value;
        if (owner.Contains('.') && !text.Contains("github.com", StringComparison.OrdinalIgnoreCase)) return null; // some other website
        return owner + "/" + m.Groups[2].Value;
    }
}
