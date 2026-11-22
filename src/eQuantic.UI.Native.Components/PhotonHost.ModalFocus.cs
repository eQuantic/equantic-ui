using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// A modal layer's focus on the way in and on the way out (#1811), as the web's focus trap does
/// (<c>dom/focus-trap.ts</c>). A layer that opens records what had the focus and takes it: the control
/// marked <see cref="Pressable.InitialFocus"/>, else its first stop, unless an Autofocus inside already
/// took it. A layer that closes gives the focus back to what it recorded, however it closed. When that
/// control is gone (the row a dialog deleted is what opened it), the focus goes to the stop that now
/// stands at its place among the stops beneath the layer: the same control of the next row, or the last
/// one when it was the last. That is the one place a control inherits the focus of another that left,
/// where #976 keeps every other case from it, because WAI-ARIA's dialog pattern asks for a logical
/// element and the page's start is none.
/// </summary>
public sealed partial class PhotonHost
{
    /// <summary>A modal layer shown, what had the focus when it opened, that control's place among the
    /// stops beneath the layer then, and whether the focus was seen.</summary>
    private readonly record struct ModalReturn(Element Layer, Element? At, int Place, bool Visible);

    /// <summary>The modal layers shown, bottom to top, each with the focus it gives back.</summary>
    private readonly List<ModalReturn> _modalReturns = [];

    /// <summary>Records the layers this frame opened and gives the focus back for those it closed:
    /// the outermost's return wins when several close at once, as they close in one pass on the web.
    /// A frame whose modal layers are the ones recorded, nearly every frame, allocates nothing here.</summary>
    private void KeepModalFocus()
    {
        var frame = _lastFrame!;
        if (ShowsTheRecordedLayers(frame)) return;
        var open = frame.ModalLayerRoots();
        ModalReturn? back = null;
        for (var i = _modalReturns.Count - 1; i >= 0; i--)
        {
            if (IndexOf(open, _modalReturns[i].Layer) >= 0) continue;
            back = _modalReturns[i];
            _modalReturns.RemoveAt(i);
        }
        if (back is { } closed) GiveFocusBack(closed, frame, open);

        foreach (var layer in open)
        {
            if (Recorded(layer)) continue;
            var at = _textAt ?? _focusedAt;
            var place = -1;
            if (at is not null)
            {
                var beneath = StopsBeneath(frame, open, layer);
                for (var i = 0; i < beneath.Count && place < 0; i++)
                    if (ReferenceEquals(beneath[i].Element, at)) place = i;
            }
            _modalReturns.Add(new ModalReturn(layer, at, place, _focusVisible));
            FocusInto(layer, frame);
        }
    }

    /// <summary>Whether the modal and open layers of <paramref name="frame"/> are the recorded ones, in order.</summary>
    private bool ShowsTheRecordedLayers(RealizeResult frame)
    {
        var shown = 0;
        for (var i = 0; i < frame.OverlayLayers.Count; i++)
        {
            if (frame.OverlayLayers[i] is not { Modal: true, Open: true } || frame.OverlayRoots[i].Element is not { } root)
                continue;
            if (shown >= _modalReturns.Count || !ReferenceEquals(_modalReturns[shown].Layer, root)) return false;
            shown++;
        }
        return shown == _modalReturns.Count;
    }

    private bool Recorded(Element layer)
    {
        foreach (var shown in _modalReturns)
            if (ReferenceEquals(shown.Layer, layer)) return true;
        return false;
    }

    private static int IndexOf(List<Element> layers, Element layer)
    {
        for (var i = 0; i < layers.Count; i++)
            if (ReferenceEquals(layers[i], layer)) return i;
        return -1;
    }

    /// <summary>
    /// The stops Tab reaches beneath <paramref name="above"/>: those of the topmost modal layer under it,
    /// or the page's when none is, never one inside another modal layer. Null for <paramref name="above"/>
    /// is the stops beneath every layer still shown.
    /// </summary>
    private static List<FocusStop> StopsBeneath(RealizeResult frame, List<Element> open, Element? above)
    {
        var below = above is null ? open.Count : IndexOf(open, above);
        var holder = below > 0 ? open[below - 1] : null;
        var stops = new List<FocusStop>();
        foreach (var stop in frame.FocusStops)
        {
            if (holder is not null && !Holds(holder, stop.Element)) continue;
            var inAbove = false;
            for (var i = below; i < open.Count && !inAbove; i++) inAbove = Holds(open[i], stop.Element);
            if (!inAbove) stops.Add(stop);
        }
        return stops;
    }

    /// <summary>The focus a closed layer recorded, back on its control, or on the stop now at its place.</summary>
    private void GiveFocusBack(ModalReturn closed, RealizeResult frame, List<Element> open)
    {
        if (closed.At is null) return;
        var beneath = StopsBeneath(frame, open, null);
        if (beneath.Count == 0) return;
        var target = closed.At.IsActive
            ? beneath.Find(stop => ReferenceEquals(stop.Element, closed.At))
            : default;
        if (target.Element is null)
        {
            if (closed.Place < 0) return;
            target = beneath[Math.Min(closed.Place, beneath.Count - 1)];
        }
        Land(target);
        _focusVisible = closed.Visible;
    }

    /// <summary>The focus moved into a layer that just opened, unless it is there already.</summary>
    private void FocusInto(Element layer, RealizeResult frame)
    {
        if ((_textAt ?? _focusedAt) is { } at && Holds(layer, at)) return;
        FocusStop? first = null;
        foreach (var stop in frame.FocusStops)
        {
            if (!Holds(layer, stop.Element)) continue;
            if (stop.Pressable is { InitialFocus: true })
            {
                first = stop;
                break;
            }
            first ??= stop;
        }
        var visible = _focusVisible;
        if (first is { } target)
        {
            Land(target);
            _focusVisible = visible;
            return;
        }
        // A layer with nothing to stop on holds the focus all the same: nothing behind it keeps it.
        EndEditing();
        ClearFocus();
    }
}
