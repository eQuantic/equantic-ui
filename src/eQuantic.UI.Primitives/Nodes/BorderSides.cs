namespace eQuantic.UI.Primitives;

/// <summary>
/// Which edges of a box its border draws on.
/// <para>
/// Start and End rather than Left and Right, matching <c>EdgeInsets</c> and for the same reason:
/// they mirror in a right-to-left reading, and an accent bar that stays on the left when the text
/// flows the other way is on the wrong side of it.
/// </para>
/// </summary>
[Flags]
public enum BorderSides
{
    None = 0,
    Top = 1,
    End = 2,
    Bottom = 4,
    Start = 8,

    /// <summary>Every edge — the default, and the only value with a corner radius worth trusting
    /// (see <c>BoxStyle.BorderSides</c>).</summary>
    All = Top | End | Bottom | Start,
}
