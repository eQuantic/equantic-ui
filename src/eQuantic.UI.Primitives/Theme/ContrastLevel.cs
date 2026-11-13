namespace eQuantic.UI.Primitives;

/// <summary>
/// How much contrast the person looking at the screen asked their system for: the second axis a
/// <see cref="ColorToken"/> resolves on, beside <see cref="ThemeMode"/> (Foundations §01, "Contrast
/// levels"). The input is the operating system's own setting, read by the target the way the mode
/// is: Increase Contrast on iOS and macOS, the contrast level on Android, a high-contrast theme on
/// Windows, and <c>prefers-contrast: more</c> on the web. An app states nothing to get it.
/// <para>
/// Flutter carries the same fact as <c>MediaQueryData.highContrast</c>, and its
/// <c>CupertinoDynamicColor</c> holds a high-contrast color beside each brightness, the way a
/// <see cref="ColorToken"/> holds <see cref="ColorToken.MoreLight"/> and
/// <see cref="ColorToken.MoreDark"/> beside <see cref="ColorToken.Light"/> and
/// <see cref="ColorToken.Dark"/>.
/// </para>
/// </summary>
public enum ContrastLevel : byte
{
    /// <summary>The palette as designed.</summary>
    Standard = 0,

    /// <summary>Every pair one APCA level up: borders and muted text step up, never below the WCAG 2
    /// targets.</summary>
    More = 1,
}
