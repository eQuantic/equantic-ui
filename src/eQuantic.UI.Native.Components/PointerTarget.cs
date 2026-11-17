using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// One node of a frame as the listeners' hit test sees it (#1562), in paint order: what the web's
/// <c>elementsFromPoint</c> is to the browser's controller. A <see cref="Listener"/> entry hears; a
/// <see cref="Solid"/> one draws or answers and so is hit (<see cref="ListenerHits"/>).
/// </summary>
/// <param name="Bounds">The node's box on screen, whole: a listener's positions are measured from it.</param>
/// <param name="Visible">The part of it the clips leave, which a pointer lands on.</param>
/// <param name="End">The index past the node's subtree in the frame's entries: what lies between is inside it.</param>
/// <param name="Solid">Whether the node painted something of its own or registered input.</param>
/// <param name="Listener">The listener this entry is, or null.</param>
/// <param name="Element">Its position, by which a pointer followed from its down finds it in a later frame.</param>
internal readonly record struct PointerTarget(
    Rect Bounds, Rect Visible, int End, bool Solid, Listener? Listener, Element? Element);
