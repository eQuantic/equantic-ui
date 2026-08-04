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
    /// </summary>
    public static bool Write(string assemblyPath, string plistPath)
    {
        var declared = Read(assemblyPath);
        var keys = declared
            .Select(pair => (Key: AppleKey(pair.Capability), pair.Reason))
            .Where(entry => entry.Key is not null)
            .ToArray();

        if (keys.Length == 0) return false;

        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
        File.WriteAllText(plistPath, PropertyListWriter.Document(plist =>
        {
            foreach (var (key, reason) in keys) plist.String(key!, reason);
        }));
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
            var capability = blob.ReadInt32();
            var reason = blob.ReadSerializedString();
            if (reason is not null) declared.Add((CapabilityName(capability), reason));
        }

        return declared;
    }

    /// <summary>The attribute's own type name, whichever way it was referenced.</summary>
    private static string? NameOf(MetadataReader metadata, CustomAttribute attribute) =>
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

    /// <summary>The enum's members, in their declared order — the value in the blob is the ordinal.</summary>
    private static string CapabilityName(int value) => value switch
    {
        0 => "Camera",
        1 => "Microphone",
        2 => "PhotoLibrary",
        3 => "PhotoLibraryAdd",
        4 => "Location",
        5 => "Motion",
        6 => "Biometrics",
        7 => "Contacts",
        8 => "Notifications",
        _ => value.ToString(),
    };

    /// <summary>Apple's spellings. Historical, not derivable — a photo library and adding to one
    /// are different keys, and Face ID has its own.</summary>
    private static string? AppleKey(string capability) => capability switch
    {
        "Camera" => "NSCameraUsageDescription",
        "Microphone" => "NSMicrophoneUsageDescription",
        "PhotoLibrary" => "NSPhotoLibraryUsageDescription",
        "PhotoLibraryAdd" => "NSPhotoLibraryAddUsageDescription",
        "Location" => "NSLocationWhenInUseUsageDescription",
        "Motion" => "NSMotionUsageDescription",
        "Biometrics" => "NSFaceIDUsageDescription",
        "Contacts" => "NSContactsUsageDescription",
        _ => null,   // Notifications is a runtime prompt on Apple platforms, not a manifest key.
    };
}
