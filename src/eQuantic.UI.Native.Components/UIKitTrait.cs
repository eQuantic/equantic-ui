namespace eQuantic.UI.Native.Components;

/// <summary>
/// The UIKit traits the iOS bridge speaks — NAMED here, valued there. A closed set that changes only
/// when somebody decides to say something new, which is exactly why it sits beside
/// <see cref="Primitives.SemanticRole"/> instead of being folded into it.
/// <para>
/// A role's trait cannot be written in <see cref="NativeRole"/> as UIKit's own
/// <c>UIAccessibilityTrait</c>: that type comes from the iOS binding, and this assembly is the one
/// every shell reads. Transcribing its BIT VALUES here instead would be copying constants the
/// binding already holds correctly — so the trait is named here and spelled in the one file that
/// has the real ones.
/// </para>
/// <para>
/// The names are UIKit's on purpose: this type IS the transcription of a target's vocabulary, which
/// is the one case the neutral-vocabulary rule makes an exception for.
/// </para>
/// </summary>
public enum UIKitTrait : byte
{
    /// <summary>No trait at all. A text field carries none (what identifies it is having a value and
    /// taking the keyboard) and neither does a container (UIKit expresses a group by BEING one).</summary>
    None,

    Button,

    Link,

    Image,

    /// <summary>The swipe-up/down adjust gesture — UIKit's word for a slider.</summary>
    Adjustable,

    /// <summary>A value that moves on its own: what stops VoiceOver re-announcing a progress bar
    /// sixty times a second. UIKit has no progress trait, and UIProgressView reports this one.</summary>
    UpdatesFrequently,

    /// <summary>Text that is read rather than acted on — and the ONE role allowed to claim it, which
    /// is the whole point of <see cref="NativeRole"/>.</summary>
    StaticText,
}
