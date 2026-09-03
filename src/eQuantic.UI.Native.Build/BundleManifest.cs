using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace eQuantic.UI.Build;

/// <summary>
/// What the app declared about its own BUNDLE, read straight off the compiled assembly — the third
/// of the three platform-fact families, beside <see cref="CapabilityManifest"/> (what it asks of a
/// person) and <see cref="EntitlementsManifest"/> (what it asks of the system).
/// <para>
/// These are the answers macOS reads before the app runs: the copyright line in Get Info, the
/// category Finder groups by, the oldest system it will start on, whether it takes a Dock tile,
/// the URLs it answers to. Every one of them used to be unsayable — the Info.plist the SDK writes
/// had a fixed set of keys — so an app that needed any of them had no path at all short of
/// post-processing the bundle.
/// </para>
/// </summary>
public static class BundleManifest
{
    private const string AttributeName = "PhotonBundleKeyAttribute";

    /// <summary>How a value is written. Mirrors <c>PhotonBundleValueKind</c>, which lives in
    /// Primitives and is not referenced here: this tool reads METADATA, and loading the app's
    /// framework to name an enum member would mean loading the app.</summary>
    public enum ValueKind
    {
        /// <summary>A plain string.</summary>
        Text = 0,

        /// <summary>A real boolean.</summary>
        Flag = 1,

        /// <summary>A URL scheme, collected into CFBundleURLTypes rather than written under a key.</summary>
        UrlScheme = 2,
    }

    /// <summary>One declared fact.</summary>
    public readonly record struct Fact(string Key, string Value, ValueKind Kind);

    /// <summary>
    /// The facts, in a stable order. Later declarations of the same key win, which is what the
    /// builder's own dictionary does — the generator writes them in key order, so "later" only
    /// arises when an app hand-writes the attribute.
    /// </summary>
    public static IReadOnlyList<Fact> Read(string assemblyPath)
    {
        if (!File.Exists(assemblyPath)) return [];

        using var stream = File.OpenRead(assemblyPath);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();

        var keyed = new Dictionary<string, Fact>(StringComparer.Ordinal);
        var schemes = new List<string>();

        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (CapabilityManifest.NameOf(metadata, attribute) != AttributeName) continue;

            // Two strings and an enum, in that order — decoded by hand for the same reason the
            // other two manifests do it: a typed decoder would need the attribute's assembly
            // loaded, and this tool must never load the app it is packaging.
            var blob = metadata.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 1) continue;                       // prolog
            var key = blob.ReadSerializedString();
            var value = blob.ReadSerializedString();
            if (blob.RemainingBytes < sizeof(int)) continue;
            var kind = (ValueKind)blob.ReadInt32();                     // the enum's underlying type

            if (value is null) continue;
            if (kind == ValueKind.UrlScheme)
            {
                if (!schemes.Contains(value, StringComparer.Ordinal)) schemes.Add(value);
                continue;
            }

            if (!string.IsNullOrEmpty(key)) keyed[key!] = new Fact(key!, value, kind);
        }

        var facts = keyed.Values.OrderBy(fact => fact.Key, StringComparer.Ordinal).ToList();
        facts.AddRange(schemes.Select(scheme => new Fact("", scheme, ValueKind.UrlScheme)));
        return facts;
    }
}
