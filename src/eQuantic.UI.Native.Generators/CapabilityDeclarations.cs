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

    internal static readonly DiagnosticDescriptor NoApiUsesIt = new(
        "EQ3008", "No API uses this capability",
        "No API of the SDK uses {0} yet, and App Review and Google Play both reject an app that asks for a "
        + "permission it does not use. Remove the declaration until the API lands.",
        "eQuantic.UI", DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>
    /// The capabilities the enum names and no contract realizes on any target (#779). Their members stay,
    /// since an enum is append-only (<c>EnumValueAbiTests</c>), and each goes from here with the API that
    /// uses it, which also says what it asks of each platform: <c>PhotoLibraryAdd</c> asked for
    /// <c>WRITE_EXTERNAL_STORAGE</c>, which has no effect from Android 11.
    /// </summary>
    internal static bool HasNoRealization(string capability) =>
        capability is "Microphone" or "PhotoLibraryAdd" or "Contacts" or "Notifications";

    /// <summary>Whether a syntax node is worth looking at — cheap, and runs on every keystroke.</summary>
    internal static bool MightDeclare(SyntaxNode node) =>
        BuilderCall.MethodName(node) is { } name && name.StartsWith("Use", System.StringComparison.Ordinal);

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
        "PhotoLibrary" => "NSPhotoLibraryUsageDescription",
        "Location" => "NSLocationWhenInUseUsageDescription",
        "Motion" => "NSMotionUsageDescription",
        "Biometrics" => "NSFaceIDUsageDescription",
        _ => null,
    };

    /// <summary>The permissions Android reads. Some capabilities are several.</summary>
    internal static IEnumerable<string> AndroidPermissions(string capability) => capability switch
    {
        "Camera" => new[] { "android.permission.CAMERA" },
        // Android 13 split the media permissions by type; the old one still covers 12 and below.
        "PhotoLibrary" => new[] { "android.permission.READ_MEDIA_IMAGES" },
        "Location" => new[] { "android.permission.ACCESS_FINE_LOCATION", "android.permission.ACCESS_COARSE_LOCATION" },
        "Motion" => new[] { "android.permission.HIGH_SAMPLING_RATE_SENSORS" },
        "Biometrics" => new[] { "android.permission.USE_BIOMETRIC" },
        "NetworkStatus" => new[] { "android.permission.ACCESS_NETWORK_STATE" },
        _ => Enumerable.Empty<string>(),
    };
}
