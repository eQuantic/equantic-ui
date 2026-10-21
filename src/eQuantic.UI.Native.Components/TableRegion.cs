using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// A <see cref="TableViewport"/>'s window, where the host routes the wheel, a finger's pan and a
/// scrollbar press to the scroll model the viewport carries. <paramref name="Visible"/> is the part of
/// <paramref name="Bounds"/> on screen, which is all a press can land on; null where nothing clips it.
/// </summary>
public readonly record struct TableRegion(Rect Bounds, TableViewport Viewport, Element Element, Rect? Visible = null);
