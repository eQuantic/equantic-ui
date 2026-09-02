using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using eQuantic.UI.Codegen;

namespace eQuantic.UI.Build;

/// <summary>
/// Turns the app's <c>[assembly: PhotonEntitlement(…)]</c> declarations into the
/// <c>.entitlements</c> file <c>codesign</c> reads.
/// <para>
/// The same shape as <see cref="CapabilityManifest"/>, for the same reason: the fact is declared
/// once in C# and the SDK writes the platform's file, so nobody edits a plist. What differs is who
/// is being asked — a capability asks the user at run time, an entitlement asks the system at SIGN
/// time, and an app that needs one without having it is killed rather than refused.
/// </para>
/// </summary>
public static class EntitlementsManifest
{
    private const string AttributeName = "PhotonEntitlementAttribute";

    /// <summary>Writes the entitlements plist, or returns false when the app declared none — in
    /// which case the signing step passes no <c>--entitlements</c> at all, which is correct: an
    /// empty entitlements file is not the same as no file, and signing with one grants nothing
    /// while still changing the signature.</summary>
    /// <param name="alsoRequired">
    /// What the RUNTIME needs, which the SDK knows and the app should never have to. A
    /// framework-dependent .NET app under the hardened runtime cannot load its own runtime's dylibs
    /// without disabling library validation, and cannot execute a single JIT-compiled method
    /// without the JIT entitlement — neither is a choice an app makes, both are consequences of
    /// being .NET on macOS. The SDK derives them from what it already knows (hardened? AOT?
    /// self-contained?) and passes them here, so they land in the SAME file by the SAME writer
    /// rather than being merged by whoever signs.
    /// </param>
    public static bool Write(string assemblyPath, string plistPath,
        IEnumerable<string>? alsoRequired = null)
    {
        var declared = Read(assemblyPath).Concat(alsoRequired ?? [])
            .Where(key => key.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
        if (declared.Count == 0)
        {
            // DELETE, not just "write nothing": obj/ survives between builds, and the signing step
            // decides by whether the file exists. Leaving yesterday's file there would sign
            // yesterday's entitlements into an app whose declarations were removed — the app would
            // keep a permission its source no longer asks for, which nobody would ever notice.
            if (File.Exists(plistPath)) File.Delete(plistPath);
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
        File.WriteAllText(plistPath, PropertyListWriter.Document(plist =>
        {
            // Every entitlement this SDK knows how to declare is a boolean grant. Keys that take a
            // string or an array (a temporary-exception path, an application group) are not
            // expressible here yet, and saying so is better than writing `<true/>` under a key that
            // means something else.
            foreach (var entitlement in declared) plist.Bool(entitlement, true);
        }));
        return true;
    }

    /// <summary>The declarations, read straight from the assembly's metadata — the tool never loads
    /// the app, which would mean running it.</summary>
    public static IReadOnlyList<string> Read(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();
        var declared = new List<string>();

        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (CapabilityManifest.NameOf(metadata, attribute) != AttributeName) continue;

            // One string: Apple's own key. Decoded by hand for the same reason the capability
            // manifest does it — a typed decoder would need the attribute's assembly loaded.
            var blob = metadata.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 1) continue;              // prolog
            if (blob.ReadSerializedString() is { Length: > 0 } key && !declared.Contains(key))
                declared.Add(key);
        }

        // Sorted, so the file is the same file for the same declarations: an entitlements plist
        // that reshuffles changes the signature, and a signature that changes for no reason is a
        // diff nobody can review and a TCC grant nobody keeps.
        declared.Sort(StringComparer.Ordinal);
        return declared;
    }
}
