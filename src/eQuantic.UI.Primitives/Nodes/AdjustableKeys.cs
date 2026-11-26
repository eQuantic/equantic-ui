namespace eQuantic.UI.Primitives;

/// <summary>
/// An ARROW KEY, as the step it means for an <see cref="Adjustable"/>: +1 to the next item or a higher value,
/// -1 to the previous item or a lower one, by the adjustable's role and the direction it reads in. The left and
/// right arrows step back and forward for every role in a left-to-right reading, and swap in a right-to-left one,
/// where the end of the row is on the left (#1569). The vertical arrows split by role family, WAI-ARIA's own
/// split, whatever the direction: a slider's or a separator's vertical axis is VALUE, so up increases, and a tab
/// list's or a radio group's is READING ORDER, so down is the next item.
/// <para>
/// It sits beside <see cref="NavigableKeys"/>, and for the same reason: the web realizer and
/// <c>PhotonHost.KeyDown</c> read the same key names, so they read one table. Photon had a mapping of its own,
/// up as +1 for every role, so a vertical radio group or tab strip stepped backwards on Photon (#1873). Its
/// twin lives in <c>lowering.ts</c>'s <c>lowerAdjustable</c>.
/// </para>
/// <para>
/// A key that is not an arrow answers null and reaches the page untouched.
/// </para>
/// </summary>
[ServerOnly]
public static class AdjustableKeys
{
    /// <summary>The step <paramref name="key"/> takes on an adjustable of <paramref name="role"/> that reads in
    /// <paramref name="direction"/>, or null when the key is not one of the four arrows.</summary>
    public static int? Step(string key, AdjustableRole role, TextDirection direction)
    {
        var downIsNext = role is not (AdjustableRole.Slider or AdjustableRole.Separator);
        var reversed = direction == TextDirection.RightToLeft;
        return key switch
        {
            "ArrowRight" => reversed ? -1 : 1,
            "ArrowLeft" => reversed ? 1 : -1,
            "ArrowUp" => downIsNext ? -1 : 1,
            "ArrowDown" => downIsNext ? 1 : -1,
            _ => null,
        };
    }
}
