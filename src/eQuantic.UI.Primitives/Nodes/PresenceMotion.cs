namespace eQuantic.UI.Primitives;

/// <summary>How a <see cref="Presence"/> subtree ENTERS when it first appears (spec §06).</summary>
public enum PresenceMotion : byte
{
    /// <summary>Opacity 0→1 over Motion.Base — dialog cards and scrims.</summary>
    Fade = 0,
    /// <summary>Rise from <see cref="Presence.SlideDistance"/> below while fading in — sheets, toasts.</summary>
    SlideUp = 1,
}
