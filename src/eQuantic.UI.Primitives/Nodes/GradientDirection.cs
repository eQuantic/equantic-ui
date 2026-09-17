namespace eQuantic.UI.Primitives;

/// <summary>Axis of a 2-stop linear gradient — the CSS keyword pair the web realizer emits; the
/// native realizer maps it to <c>Paint.Linear</c> points over the box bounds.</summary>
public enum GradientDirection : byte
{
    ToRight = 0,
    ToBottom = 1,

    /// <summary>Top-left → bottom-right. The diagonal axis every "tinted tile" uses (CSS
    /// <c>to bottom right</c>); native runs the SAME <c>Paint.Linear</c> primitive with the
    /// diagonal endpoints — no engine or shader change, the axis was always arbitrary points.</summary>
    ToBottomRight = 2,

    /// <summary>Top-right → bottom-left (CSS <c>to bottom left</c>).</summary>
    ToBottomLeft = 3,
}
