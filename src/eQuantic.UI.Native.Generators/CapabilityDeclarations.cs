using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace eQuantic.UI.Native.Generators;

/// <summary>What an app asked of the device, and the sentence it gave for it.</summary>
internal readonly struct CapabilityDeclaration(string capability, string reason, Location location)
{
    public string Capability { get; } = capability;
    public string Reason { get; } = reason;
    public Location Location { get; } = location;
}

/// <summary>
/// Reads <c>builder.Capabilities.UseCamera("…")</c> out of the app's own source.
/// <para>
/// A permission has to be declared in a manifest the OS reads at INSTALL time — long before any of
/// this code runs — so something has to know at compile time. Reading the fluent calls is what lets
/// the app say it once, in the same place and the same shape as everything else it configures, and
/// still have the platform files written for it.
/// </para>
/// <para>
/// The price is that the reason must be a constant. That is worth stating plainly rather than
/// working around: a reason built at run time could not reach the manifest at all, so a missing
/// permission would be discovered by a user, on a device, when the camera did not open.
/// </para>
/// </summary>
internal static class CapabilityDeclarations
{
    private const string BuilderType = "eQuantic.UI.Native.Hosting.PhotonCapabilitiesBuilder";

    internal static readonly DiagnosticDescriptor ReasonMustBeConstant = new(
        "EQ3003", "A capability's reason must be a constant",
        "The reason for {0} is built at run time, and a permission is written into the app's "
        + "manifest at BUILD time — so this one would never reach the device. Use a literal or a "
        + "const string.",
        "eQuantic.UI", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>Whether a syntax node is worth looking at — cheap, and runs on every keystroke.</summary>
    internal static bool MightDeclare(SyntaxNode node) =>
        node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: var name },
        } && name.StartsWith("Use", System.StringComparison.Ordinal);

    /// <summary>The declaration a call makes, or null when the call is something else entirely.</summary>
    internal static CapabilityDeclaration? Read(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetOperation(context.Node) is not IInvocationOperation invocation)
            return null;
        if (invocation.TargetMethod.ContainingType?.ToDisplayString() != BuilderType) return null;

        // Both shapes reach here: the named `UseCamera(reason)` and the general
        // `Use(DeviceCapability.Camera, reason)`.
        var arguments = invocation.Arguments;
        var (capability, reason) = arguments.Length switch
        {
            1 => (invocation.TargetMethod.Name.Substring(3), arguments[0]),
            2 => (NameOf(arguments[0]), arguments[1]),
            _ => (null, null),
        };
        if (capability is null || reason is null) return null;

        var location = context.Node.GetLocation();
        return reason.Value.ConstantValue is { HasValue: true, Value: string text }
            ? new CapabilityDeclaration(capability, text, location)
            : new CapabilityDeclaration(capability, string.Empty, location);
    }

    /// <summary>The enum member a `Use(DeviceCapability.X, …)` names.</summary>
    private static string? NameOf(IArgumentOperation argument) =>
        argument.Value is IFieldReferenceOperation { Field.ContainingType.Name: "DeviceCapability" } field
            ? field.Field.Name
            : null;

    /// <summary>
    /// The keys an Apple platform reads. The name is not derivable from the capability — Apple's
    /// own spellings are historical (a photo library is "NSPhotoLibraryUsageDescription", adding to
    /// it is a different key entirely) — so they are simply stated.
    /// </summary>
    internal static string? AppleKey(string capability) => capability switch
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

    /// <summary>The permissions Android reads. Some capabilities are several.</summary>
    internal static IEnumerable<string> AndroidPermissions(string capability) => capability switch
    {
        "Camera" => new[] { "android.permission.CAMERA" },
        "Microphone" => new[] { "android.permission.RECORD_AUDIO" },
        // Android 13 split the media permissions by type; the old one still covers 12 and below.
        "PhotoLibrary" => new[] { "android.permission.READ_MEDIA_IMAGES" },
        "PhotoLibraryAdd" => new[] { "android.permission.WRITE_EXTERNAL_STORAGE" },
        "Location" => new[] { "android.permission.ACCESS_FINE_LOCATION", "android.permission.ACCESS_COARSE_LOCATION" },
        "Motion" => new[] { "android.permission.HIGH_SAMPLING_RATE_SENSORS" },
        "Biometrics" => new[] { "android.permission.USE_BIOMETRIC" },
        "Contacts" => new[] { "android.permission.READ_CONTACTS" },
        "Notifications" => new[] { "android.permission.POST_NOTIFICATIONS" },
        _ => Enumerable.Empty<string>(),
    };
}
