using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// A <see cref="Sortable"/> list or a <see cref="SortableItem"/> of one, where the frame drew it (#1537): what
/// the host lifts an item by, finds the list under the pointer by, and places the item among the others by.
/// <para>
/// Registered WHEREVER it is, as a link is: <see cref="Bounds"/> is the whole box on screen and
/// <see cref="Visible"/> the part the clips around it leave, empty for an item scrolled out of sight. A press
/// and the list under the pointer read the part that shows; a place counts every item, the hidden ones too,
/// or an item dropped below a list's first visible row would land at its top.
/// </para>
/// </summary>
/// <param name="Bounds">The box on screen, whole.</param>
/// <param name="Visible">The part of it on screen.</param>
/// <param name="Element">Its position, which an item's lift is kept on.</param>
/// <param name="Node">The <see cref="Sortable"/> or the <see cref="SortableItem"/>.</param>
internal readonly record struct SortRegion(Rect Bounds, Rect Visible, Element Element, VisualNode Node);
