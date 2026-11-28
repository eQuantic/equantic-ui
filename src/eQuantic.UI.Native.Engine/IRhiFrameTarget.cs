using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Engine;

/// <summary>
/// Where a frame's LAST pass draws, and what submitting that pass means (#1952). Every pass before it
/// (a group's layer, a backdrop's content and the levels of its blur) draws into a target the
/// renderer takes from its own pool; only the last one draws here. Offscreen, this is a render target
/// the caller reads back, so its submit waits. On a window it is the drawable or the swapchain image,
/// and its submit shows the frame.
/// <para>
/// It exists so that a window and an offscreen target take ONE path through
/// <see cref="RhiRenderer.Render(DisplayList, IRhiFrameTarget)"/>. Before it, the window paths opened
/// their own pass and encoded into it, so a group's opacity blended each of its draws on its own and a
/// backdrop drew no blur there, while the goldens, which render offscreen, saw neither.
/// </para>
/// </summary>
public interface IRhiFrameTarget
{
    /// <summary>The frame's width in device pixels, which every target the frame draws into shares.</summary>
    int Width { get; }

    /// <summary>The frame's height in device pixels.</summary>
    int Height { get; }

    /// <summary>Begins the frame's last pass, cleared to <paramref name="clearColor"/>.</summary>
    IRhiCommandList Begin(Color clearColor);

    /// <summary>Submits the pass <see cref="Begin"/> opened: shows the frame on a window, or completes it offscreen.</summary>
    void Submit(IRhiCommandList commands);
}
