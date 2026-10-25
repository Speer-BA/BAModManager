using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace BAModManager.Core;

/// <summary>What a mod's DLL says about itself: [MelonInfo(...)] and the usual assembly attributes.</summary>
public sealed record DllMeta(string? Name, string? Version, string? Author, string? Description, string? DownloadLink);

/// <summary>
/// Reads a .NET DLL's metadata without loading it (safe: no mod code runs).
/// Picks up MelonLoader's [MelonInfo(typeof(X), "Name", "1.0.0", "Author", "link")],
/// [AssemblyDescription], [AssemblyTitle], [AssemblyCompany] and the version attributes.
/// </summary>
public static class DllMetaReader
{
    private static readonly Dictionary<string, (DateTime Stamp, DllMeta? Meta)> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Lock = new();

    public static DllMeta? Read(string path)
    {
        try
        {
            var stamp = File.GetLastWriteTimeUtc(path);
            lock (Lock)
            {
                if (Cache.TryGetValue(path, out var hit) && hit.Stamp == stamp) return hit.Meta;
            }
            var meta = ReadUncached(path);
            lock (Lock) Cache[path] = (stamp, meta);
            return meta;
        }
        catch
        {
            return null;
        }
    }

    private static DllMeta? ReadUncached(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var pe = new PEReader(fs);
        if (!pe.HasMetadata) return null;
        var md = pe.GetMetadataReader();
        if (!md.IsAssembly) return null;

        string? melonName = null, melonVersion = null, melonAuthor = null, melonLink = null;
        string? description = null, title = null, company = null, infoVersion = null, fileVersion = null;

        var assembly = md.GetAssemblyDefinition();
        foreach (var handle in assembly.GetCustomAttributes())
        {
            var attr = md.GetCustomAttribute(handle);
            var typeName = AttributeTypeName(md, attr.Constructor);
            if (typeName == null) continue;

            List<object?>? args;
            try
            {
                args = DecodeArgs(md, attr);
            }
            catch
            {
                continue;
            }
            if (args == null || args.Count == 0) continue;

            switch (typeName)
            {
                case "MelonInfoAttribute":
                    // (Type type, string name, string version, string author, string downloadLink = null)
                    // or (Type type, string name, int major, int minor, int patch, [string identifier,] string author, [string link])
                    var strings = args.Skip(1).ToList();
                    melonName = strings.FirstOrDefault() as string;
                    if (strings.Count > 1 && strings[1] is string v)
                    {
                        melonVersion = v;
                        melonAuthor = strings.Count > 2 ? strings[2] as string : null;
                        melonLink = strings.Count > 3 ? strings[3] as string : null;
                    }
                    else
                    {
                        // int version overloads: (type, name, major, minor, patch, [identifier,] author, link)
                        var ints = strings.Skip(1).TakeWhile(a => a is int).Cast<int>().ToList();
                        if (ints.Count > 0) melonVersion = string.Join('.', ints);
                        var tail = strings.Skip(1 + ints.Count).ToList();
                        if (args.Count >= 8 && tail.Count >= 3)
                        {
                            if (tail[0] is string { Length: > 0 } tag) melonVersion += "-" + tag;
                            melonAuthor = tail[1] as string;
                            melonLink = tail[2] as string;
                        }
                        else
                        {
                            melonAuthor = tail.Count > 0 ? tail[0] as string : null;
                            melonLink = tail.Count > 1 ? tail[1] as string : null;
                        }
                    }
                    break;
                case "AssemblyDescriptionAttribute": description = args[0] as string; break;
                case "AssemblyTitleAttribute": title = args[0] as string; break;
                case "AssemblyCompanyAttribute": company = args[0] as string; break;
                case "AssemblyInformationalVersionAttribute": infoVersion = args[0] as string; break;
                case "AssemblyFileVersionAttribute": fileVersion = args[0] as string; break;
            }
        }

        static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        var version = Clean(melonVersion)
                      ?? Clean(infoVersion?.Split('+')[0])
                      ?? Clean(fileVersion)
                      ?? (assembly.Version.Major + assembly.Version.Minor + assembly.Version.Build > 0 ? assembly.Version.ToString(3) : null);
        var name = Clean(melonName) ?? Clean(title);
        var author = Clean(melonAuthor) ?? Clean(company);
        if (name != null && description != null && description.Trim() == name) description = null; // VS default: description = title
        return new DllMeta(name, version, author, Clean(description), Clean(melonLink));
    }

    private static string? AttributeTypeName(MetadataReader md, EntityHandle ctor)
    {
        switch (ctor.Kind)
        {
            case HandleKind.MemberReference:
                var parent = md.GetMemberReference((MemberReferenceHandle)ctor).Parent;
                if (parent.Kind == HandleKind.TypeReference)
                    return md.GetString(md.GetTypeReference((TypeReferenceHandle)parent).Name);
                if (parent.Kind == HandleKind.TypeDefinition)
                    return md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)parent).Name);
                return null;
            case HandleKind.MethodDefinition:
                var type = md.GetMethodDefinition((MethodDefinitionHandle)ctor).GetDeclaringType();
                return md.GetString(md.GetTypeDefinition(type).Name);
            default:
                return null;
        }
    }

    /// <summary>
    /// Decodes the constructor arguments of an attribute. Only strings, Type (stored as its name),
    /// int and bool are supported - enough for the attributes above. Returns null for anything else.
    /// </summary>
    private static List<object?>? DecodeArgs(MetadataReader md, CustomAttribute attr)
    {
        // Parameter types from the constructor's signature
        BlobHandle sigHandle = attr.Constructor.Kind switch
        {
            HandleKind.MemberReference => md.GetMemberReference((MemberReferenceHandle)attr.Constructor).Signature,
            HandleKind.MethodDefinition => md.GetMethodDefinition((MethodDefinitionHandle)attr.Constructor).Signature,
            _ => default,
        };
        if (sigHandle.IsNil) return null;

        var sig = md.GetBlobReader(sigHandle);
        sig.ReadSignatureHeader();
        int count = sig.ReadCompressedInteger();
        sig.ReadSignatureTypeCode(); // return type (void)

        var kinds = new List<SignatureTypeCode>();
        for (int i = 0; i < count; i++)
        {
            var code = sig.ReadSignatureTypeCode();
            if (code is SignatureTypeCode.TypeHandle)
            {
                // class/valuetype: System.Type is the only one we expect here
                sig.ReadTypeHandle();
            }
            else if (code is not (SignatureTypeCode.String or SignatureTypeCode.Int32 or SignatureTypeCode.Boolean))
            {
                return null;
            }
            kinds.Add(code);
        }

        var blob = md.GetBlobReader(attr.Value);
        if (blob.Length < 2 || blob.ReadUInt16() != 1) return null; // prolog

        var values = new List<object?>();
        foreach (var kind in kinds)
        {
            values.Add(kind switch
            {
                SignatureTypeCode.String or SignatureTypeCode.TypeHandle => blob.ReadSerializedString(),
                SignatureTypeCode.Int32 => blob.ReadInt32(),
                SignatureTypeCode.Boolean => blob.ReadBoolean(),
                _ => null,
            });
        }
        return values;
    }
}
