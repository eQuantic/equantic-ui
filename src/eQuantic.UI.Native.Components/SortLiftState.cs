using eQuantic.UI.Native.Engine;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// What a carried <see cref="SortableItem"/>'s position keeps while the host carries it (#1537): that it is
/// lifted, which the paint walk dims it in its place for, and the copy that follows the pointer, recorded once
/// on the first frame that draws it lifted, as the web's copy is cloned once (see <c>PhotonHost.Sorting</c>).
/// </summary>
internal sealed class SortLiftState
{
    /// <summary>Whether the item is being carried.</summary>
    public bool Lifted { get; set; }

    /// <summary>Whether a copy follows a pointer: a keyboard's carry dims the item and draws no copy.</summary>
    public bool Follows { get; set; }

    /// <summary>The copy: the item as it was drawn when it was lifted, at full opacity and outside every clip,
    /// in device space. Null until a frame draws the item lifted.</summary>
    public Picture? Copy { get; set; }

    /// <summary>The copy's centre in device space, which it leans about.</summary>
    public Point Center { get; set; }
}
