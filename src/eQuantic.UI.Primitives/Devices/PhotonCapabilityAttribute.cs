namespace eQuantic.UI.Primitives;

/// <summary>
/// Declares that this app uses a device capability, and WHY.
/// <para>
/// An assembly attribute rather than a call on the builder because the BUILD needs it: the reason
/// is the sentence iOS shows in its permission sheet and Android carries in its manifest, and both
/// are written long before any code runs. One declaration in C# is the whole thing — the SDK writes
/// the platform keys, and no app author opens an Info.plist or an AndroidManifest.xml.
/// </para>
/// <para>
/// The reason is not boilerplate. It is shown to the user at the moment they decide, and Apple
/// rejects apps whose sentence says nothing. Write what the feature does for them.
/// </para>
/// </summary>
/// <para>
/// The capability crosses as its NAME, deliberately. It used to cross as the enum's ordinal, and
/// the day a member was inserted mid-enum every declaration after it silently became a different
/// one — an app that asked for Location got a Motion key in its manifest, because the writer and
/// the reader are different assemblies built at different times. A name cannot be renumbered.
/// </para>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class PhotonCapabilityAttribute(string capability, string reason) : Attribute
{
    public string Capability { get; } = capability;

    /// <summary>What the user is told, in their words, at the moment they are asked.</summary>
    public string Reason { get; } = reason;
}
