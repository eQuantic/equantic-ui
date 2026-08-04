namespace eQuantic.UI.Primitives;

/// <summary>
/// How much of the window the SYSTEM draws, and how much the app does.
/// <para>
/// A desktop head is the one surface where that is a question — a phone has no window — so it lives
/// in the vocabulary rather than in any one shell: a head on another platform answers it with that
/// platform's own machinery, and an app states the intent once.
/// </para>
/// <para>
/// Whatever the system keeps for itself is reported back as a SAFE-AREA inset, the same channel a
/// phone reports its notch through. An app that respects the reserved strip uses
/// <see cref="SafeArea"/> and needs no idea which platform it is on; one that wants to draw under
/// it simply does, exactly as it would under a status bar.
/// </para>
/// </summary>
public enum WindowChrome
{
    /// <summary>The system's title bar, in full. The content begins below it.</summary>
    Standard,

    /// <summary>
    /// No title bar: the content owns the whole window, top edge included, and the close/minimise/
    /// zoom controls float above it. The strip they occupy comes back as a top safe-area inset, so
    /// an app puts its own toolbar there and simply insets it — which is what a design that asks
    /// for "no native title bar" actually means.
    /// </summary>
    Unified,
}
