// Every assembly of this repository inside a package says what 1.0 promises it (#899).
//
// Directory.Build.targets writes a surface class into every assembly that ships, as
// [assembly: AssemblyMetadata("eQuantic.Surface", "contract" | "tool" | "experimental")]: what an app
// compiles against, a tool of the SDK, or the native track. A project that declares a public API gets
// it by its name, and a project that ships its assembly some other way says it itself: eqc, eqicon and
// the MSBuild tasks inside the SDKs, the source generators under analyzers/. What ships is what the
// packages hold, so this reads them, as CI packed them: an assembly the rule missed shows here and
// nowhere else.
//
// "Of this repository" is read from the assembly too, never listed: the SDK writes
// [assembly: AssemblyMetadata("RepositoryUrl", …)] into every assembly built here, and a package's
// dependencies (Roslyn, the Microsoft.Extensions family) carry their own. A satellite assembly holds
// resources and no code, so it has no class to say.
//
// usage: dotnet run scripts/check-package-surface.cs -- <folder of packages>
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.WriteLine("::error::usage: check-package-surface.cs <folder of packages>");
    return 2;
}

const string Repository = "https://github.com/eQuantic/equantic-ui";
string[] classes = ["contract", "tool", "experimental"];

var packages = Directory.GetFiles(args[0], "*.nupkg");
var failures = 0;
var read = 0;
var counted = new SortedDictionary<string, int>(StringComparer.Ordinal);
foreach (var package in packages)
{
    using var archive = ZipFile.OpenRead(package);
    foreach (var entry in archive.Entries.Where(entry =>
        entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        && !entry.FullName.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase)))
    {
        using var bytes = new MemoryStream();
        using (var content = entry.Open()) content.CopyTo(bytes);
        bytes.Position = 0;
        using var pe = new PEReader(bytes);
        if (!pe.HasMetadata) continue;
        var metadata = Metadata(pe.GetMetadataReader());
        if (!metadata.TryGetValue("RepositoryUrl", out var repository)
            || !repository.TrimEnd('/').Equals(Repository, StringComparison.OrdinalIgnoreCase))
            continue;

        read++;
        var where = $"{Path.GetFileName(package)}: {entry.FullName}";
        if (!metadata.TryGetValue("eQuantic.Surface", out var surface))
        {
            Console.WriteLine($"::error::{where} ships with no surface class. A project that ships its assembly sets _EqSurface (Directory.Build.targets says how)");
            failures++;
        }
        else if (!classes.Contains(surface))
        {
            Console.WriteLine($"::error::{where} says its surface class is \"{surface}\", which is none of {string.Join(", ", classes)}");
            failures++;
        }
        else
        {
            counted[surface] = counted.GetValueOrDefault(surface) + 1;
        }
    }
}

Console.WriteLine($"assemblies of this repository read: {read}, in {packages.Length} packages | "
    + string.Join(", ", counted.Select(pair => $"{pair.Key} {pair.Value}")));
if (packages.Length == 0 || read == 0)
{
    Console.WriteLine("::error::the folder holds no package with an assembly of this repository");
    failures++;
}
return failures == 0 ? 0 : 1;

// The assembly's AssemblyMetadata pairs, read from the metadata with nothing loaded: the attribute's
// two arguments are strings, so its blob is the prolog and two serialized strings.
static Dictionary<string, string> Metadata(MetadataReader reader)
{
    var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
    {
        var attribute = reader.GetCustomAttribute(handle);
        if (AttributeName(reader, attribute) != "System.Reflection.AssemblyMetadataAttribute") continue;
        var blob = reader.GetBlobReader(attribute.Value);
        if (blob.ReadUInt16() != 1) continue;
        var key = blob.ReadSerializedString();
        var value = blob.ReadSerializedString();
        if (key is not null && value is not null) pairs[key] = value;
    }
    return pairs;
}

static string AttributeName(MetadataReader reader, CustomAttribute attribute)
{
    EntityHandle type = attribute.Constructor.Kind switch
    {
        HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
        HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
        _ => default,
    };
    return type.Kind switch
    {
        HandleKind.TypeReference => Name(reader.GetTypeReference((TypeReferenceHandle)type).Namespace,
            reader.GetTypeReference((TypeReferenceHandle)type).Name),
        HandleKind.TypeDefinition => Name(reader.GetTypeDefinition((TypeDefinitionHandle)type).Namespace,
            reader.GetTypeDefinition((TypeDefinitionHandle)type).Name),
        _ => string.Empty,
    };

    string Name(StringHandle space, StringHandle name) => $"{reader.GetString(space)}.{reader.GetString(name)}";
}
