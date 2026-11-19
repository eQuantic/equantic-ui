using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// A <see cref="ContextTarget"/>, where the frame drew it (#1531): what the host answers a secondary click, a
/// finger's hold or the menu key by. <see cref="Bounds"/> is the box on screen, which a request's point is
/// measured from, and <see cref="Visible"/> the part of it the clips leave, which a pointer lands on; the keyboard
/// finds a target by the focus inside it, wherever it is drawn.
/// </summary>
/// <param name="Bounds">The box on screen, whole.</param>
/// <param name="Visible">The part of it on screen.</param>
/// <param name="Element">Its position, which the focus is measured against.</param>
/// <param name="Node">The target.</param>
internal readonly record struct ContextRegion(Rect Bounds, Rect Visible, Element Element, ContextTarget Node);
