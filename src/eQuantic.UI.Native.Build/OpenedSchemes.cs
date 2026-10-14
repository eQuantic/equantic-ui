using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace eQuantic.UI.Build;

/// <summary>
/// The schemes an app hands to the system beyond the web's and its own, as its compiled assembly
/// declares them: one <c>PhotonOpenedSchemeAttribute</c> per <c>builder.Workspace.Opens…</c> line,
/// written by the generator. Read with the metadata reader, as every declaration here is, so the
/// tool never loads the app.
/// </summary>
public static class OpenedSchemes
{
    private const string AttributeName = "PhotonOpenedSchemeAttribute";

    /// <summary>Every scheme this assembly declared it opens, once each, in ordinal order.</summary>
    public static string[] Read(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();
        var schemes = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (CapabilityManifest.NameOf(metadata, attribute) != AttributeName) continue;

            var blob = metadata.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 1) continue;              // prolog
            if (blob.ReadSerializedString() is { Length: > 0 } scheme) schemes.Add(scheme);
        }

        return [.. schemes];
    }
}
