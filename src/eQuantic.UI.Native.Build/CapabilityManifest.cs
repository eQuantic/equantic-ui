using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using eQuantic.UI.Codegen;

namespace eQuantic.UI.Build;

/// <summary>
/// Turns what an app DECLARED into the keys an Apple platform reads.
/// <para>
/// The declarations arrive as assembly attributes the generator wrote from the app's fluent calls,
/// and they are read out of the compiled assembly with the METADATA reader rather than by loading
/// it: a build tool that loads an app's assembly has to resolve every reference it has, on a target
/// framework that is not its own. Reading the metadata asks nothing of the app.
/// </para>
/// </summary>
public static class CapabilityManifest
{
    private const string AttributeName = "PhotonCapabilityAttribute";

    /// <summary>
    /// Writes the partial Info.plist for whatever this assembly declared, and returns whether there
    /// was anything to write. Apple's key names are historical rather than derivable, so they are
    /// simply stated — the same table the generator uses for Android's.
    /// <para>
    /// The URL schemes travel here too. On the Mac the whole Info.plist is the SDK's, and the
    /// bundle writer puts them in it; on a phone Apple's packaging owns that file and takes what an
    /// app declares as a PARTIAL manifest, which is this one. <paramref name="identifier"/> names
    /// the entry, as the Mac's does: it is the app's own identifier, and with none the name is
    /// left out, which Apple allows.
    /// </para>
    /// <para>
    /// And the schemes the app OPENS, as <c>LSApplicationQueriesSchemes</c>: an iPhone answers
    /// "does something open this link" only for the schemes listed there, so they are written from
    /// the same <c>builder.Workspace.Opens…</c> lines the policy is built from, with the app's own.
    /// </para>
    /// <para>
    /// And the rest of what the app stated on <c>builder.Bundle</c>, an iPhone's keys and whatever Apple
    /// adds next, but for the facts only a Mac reads (<see cref="BundleManifest.OnlyAMacReads"/>), which
    /// <paramref name="say"/> is told by name.
    /// </para>
    /// <para>
    /// With nothing declared the file is REMOVED, not merely left unwritten: the SDK merges it
    /// whenever it exists, so one left by an earlier build would go on asking for a permission, or
    /// answering a scheme, the app has since stopped declaring. And it is written only when what it
    /// says CHANGED: Apple's packaging rewrites the app's manifest whenever this file is newer, so
    /// a file touched on every build would cost every build that work.
    /// </para>
    /// </summary>
    public static bool Write(string assemblyPath, string plistPath, string? identifier = null, Action<string>? say = null)
    {
        var declared = Read(assemblyPath);
        var keys = declared
            .Select(pair => (Key: AppleKey(pair.Capability), pair.Reason))
            .Where(entry => entry.Key is not null)
            .ToArray();
        var facts = BundleManifest.Read(assemblyPath);
        var schemes = BundleManifest.Schemes(facts);
        // The schemes the app may ASK about. An iPhone answers canOpenURL only for what is listed
        // here, so the list is what the app's policy hands over beyond the web: what it opened with
        // builder.Workspace.Opens…, and its own.
        var asked = OpenedSchemes.Read(assemblyPath).Union(schemes, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        // The keys this file writes from what the app declared, which a fact stated under one of them
        // does not write again, as the Mac's Info.plist does not: a plist with a key twice is an error
        // nowhere, it is whichever the reader reaches last. A permission's sentence is the capability's.
        var written = new HashSet<string>(keys.Select(key => key.Key!), StringComparer.Ordinal);
        if (schemes.Length > 0) written.Add("CFBundleURLTypes");
        if (asked.Length > 0) written.Add("LSApplicationQueriesSchemes");
        // What the app stated on builder.Bundle, but for what only a Mac reads, which the build names
        // rather than dropping it without a word (#878).
        var stated = facts
            .Where(fact => fact.Kind != BundleManifest.ValueKind.UrlScheme && !BundleManifest.OnlyAMacReads(fact.Key))
            .Where(fact => !written.Contains(fact.Key))
            .ToArray();
        foreach (var fact in facts.Where(fact => BundleManifest.OnlyAMacReads(fact.Key)))
            say?.Invoke($"eqicon: {fact.Key} is a fact a Mac reads, and is left out of this phone's manifest");

        if (keys.Length == 0 && asked.Length == 0 && stated.Length == 0)
        {
            // Asked of the file: Delete answers a missing DIRECTORY with an exception, and a
            // first build has not made it yet.
            if (File.Exists(plistPath)) File.Delete(plistPath);
            return false;
        }

        var document = PropertyListWriter.Document(plist =>
        {
            foreach (var (key, reason) in keys) plist.String(key!, reason);
            foreach (var fact in stated)
            {
                if (fact.Kind == BundleManifest.ValueKind.Flag) plist.Bool(fact.Key, fact.Value == "true");
                else plist.String(fact.Key, fact.Value);
            }
            BundleManifest.WriteUrlTypes(plist, identifier, schemes);
            if (asked.Length > 0) plist.StringArray("LSApplicationQueriesSchemes", asked);
        });
        if (File.Exists(plistPath) && File.ReadAllText(plistPath) == document) return true;

        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
        File.WriteAllText(plistPath, document);
        return true;
    }

    /// <summary>Every capability this assembly declared, with the sentence given for it.</summary>
    public static IReadOnlyList<(string Capability, string Reason)> Read(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();
        var declared = new List<(string, string)>();

        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (NameOf(metadata, attribute) != AttributeName) continue;

            // (DeviceCapability, string): an enum is a four-byte value in the blob, and the string
            // follows it length-prefixed. Decoding by hand because a typed decoder would need the
            // attribute's own type, which lives in an assembly this tool does not load.
            var blob = metadata.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 1) continue;              // prolog
            // Two strings: the capability BY NAME (an ordinal here once turned Location into
            // Motion the day the enum grew a member mid-list) and the user-facing reason.
            var capability = blob.ReadSerializedString();
            var reason = blob.ReadSerializedString();
            if (capability is not null && reason is not null) declared.Add((capability, reason));
        }

        return declared;
    }

    /// <summary>The attribute's own type name, whichever way it was referenced.</summary>
    /// <summary>The attribute's simple name. Shared with EntitlementsManifest: one reader of the
    /// metadata shape, two callers, so a change to how attributes are found cannot fix one and
    /// miss the other.</summary>
    internal static string? NameOf(MetadataReader metadata, CustomAttribute attribute) =>
        attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => metadata.GetString(
                metadata.GetTypeReference((TypeReferenceHandle)metadata
                    .GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent).Name),
            HandleKind.MethodDefinition => metadata.GetString(
                metadata.GetTypeDefinition(metadata
                    .GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor)
                    .GetDeclaringType()).Name),
            _ => null,
        };

    /// <summary>Apple's spellings. Historical, not derivable — a photo library and adding to one
    /// are different keys, and Face ID has its own.</summary>
    public static string? AppleKey(string capability) => capability switch
    {
        "Camera" => "NSCameraUsageDescription",
        "PhotoLibrary" => "NSPhotoLibraryUsageDescription",
        "Location" => "NSLocationWhenInUseUsageDescription",
        "Motion" => "NSMotionUsageDescription",
        "Biometrics" => "NSFaceIDUsageDescription",
        _ => null,
    };
}
