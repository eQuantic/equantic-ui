namespace eQuantic.UI.Primitives;

/// <summary>
/// The buttons a pointer holds (#1562), as flags with Flutter's values (<c>kPrimaryButton</c> 1,
/// <c>kSecondaryButton</c> 2, <c>kMiddleMouseButton</c> 4, <c>kBackMouseButton</c> 8,
/// <c>kForwardMouseButton</c> 16). A finger and a pen's tip are the primary button. Nothing is held on an up,
/// which reports the buttons after the release, nor on a hover.
/// </summary>
[Flags]
public enum PointerButtons : byte
{
    /// <summary>Nothing held.</summary>
    None = 0,

    /// <summary>A mouse's main button, a finger, a pen's tip.</summary>
    Primary = 1,

    /// <summary>A mouse's secondary button, the one that asks for a menu.</summary>
    Secondary = 2,

    /// <summary>A mouse's middle button, often its wheel pressed.</summary>
    Middle = 4,

    /// <summary>A mouse's back button.</summary>
    Back = 8,

    /// <summary>A mouse's forward button.</summary>
    Forward = 16,
}
