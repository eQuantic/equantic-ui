using eQuantic.UI.Primitives;

namespace eQuantic.UI.Components;

/// <summary>
/// The scroll of a <see cref="TableViewport"/>, written once: where the grid stands under the window,
/// and every decision about how a wheel, a finger, a scrollbar and a request move it. A component that
/// draws a table creates one and keeps it across its rebuilds; each realizer feeds it what the platform
/// reported and places the cells at the offset it answers, so a wheel moves a table the same distance
/// in a browser and in a native window.
/// <para>
/// A wheel's notch and a fling GLIDE, with the constants and the rule of the native scroll views:
/// every millisecond covers the same fraction of what remains, which eats notches that arrive while
/// the last one settles, and a fling is a glide toward a target its speed bought. Under reduced motion
/// nothing glides; what was asked for still happens, at once. A finger is never smoothed: the grid is
/// under it and follows it.
/// </para>
/// </summary>
public sealed class TableScroll : ITableScrollModel
{
    /// <summary>The strip a scrollbar takes from the window, on each axis the grid overflows.</summary>
    public const float Thickness = 12f;

    /// <summary>The shortest a thumb gets, so a million rows still leave something to grab.</summary>
    public const float MinThumb = 24f;

    /// <summary>How much of the remaining distance a glide covers each millisecond.</summary>
    private const double SmoothingPerMs = 0.022;

    /// <summary>Below this a glide is over: a fraction of a point is not a frame's worth.</summary>
    private const double RestingDp = 0.5;

    /// <summary>How far a fling carries per point per millisecond of the finger's speed.</summary>
    private const double FlingReach = 90;

    /// <summary>The slowest a finger can lift and still fling, in points per millisecond.</summary>
    private const double FlingFloor = 0.05;

    /// <summary>How long a finger may rest before it lifts and still be moving, in milliseconds.</summary>
    private const double FlingWindow = 80;

    private double _across;
    private double _down;
    private double? _targetAcross;
    private double? _targetDown;
    private double _lastTime = double.NaN;

    private Size _viewport;
    private double _gridAcross;
    private double _gridDown;
    private double _pinnedAcross;
    private double _pinnedDown;
    private bool _reduceMotion;
    private bool _barDown;
    private bool _barAcross;
    private int? _honoured;

    private ScrollAxis? _dragging;
    private double _grab;
    private bool _panning;
    private Point _panAt;
    private double _panTime;
    private double _velocityAcross;
    private double _velocityDown;

    public TableOffset Offset => new(_across, _down);

    public Size Viewport => _viewport;

    private double MaxAcross => Math.Max(0, _gridAcross - _viewport.Width);

    private double MaxDown => Math.Max(0, _gridDown - _viewport.Height);

    public Size Arrange(Size window, double gridAcross, double gridDown, double pinnedAcross, double pinnedDown,
        bool reduceMotion)
    {
        _gridAcross = Math.Max(0, gridAcross);
        _gridDown = Math.Max(0, gridDown);
        _pinnedAcross = Math.Max(0, pinnedAcross);
        _pinnedDown = Math.Max(0, pinnedDown);
        _reduceMotion = reduceMotion;

        // A scrollbar takes a strip, and the strip can make the other axis overflow: asked twice.
        var down = _gridDown > window.Height;
        var across = _gridAcross > window.Width;
        if (down && !across) across = _gridAcross > window.Width - Thickness;
        if (across && !down) down = _gridDown > window.Height - Thickness;
        _barDown = down;
        _barAcross = across;
        _viewport = new Size(
            Math.Max(0f, window.Width - (down ? Thickness : 0f)),
            Math.Max(0f, window.Height - (across ? Thickness : 0f)));

        _across = Math.Clamp(_across, 0, MaxAcross);
        _down = Math.Clamp(_down, 0, MaxDown);
        if (_targetAcross is { } targetAcross) _targetAcross = Math.Clamp(targetAcross, 0, MaxAcross);
        if (_targetDown is { } targetDown) _targetDown = Math.Clamp(targetDown, 0, MaxDown);
        if (reduceMotion) Land();
        return _viewport;
    }

    public TableOffset Wheel(double across, double down, bool stepped, KeyModifiers modifiers)
    {
        // Shift turns a wheel that only moves vertically into a horizontal one; a platform that already
        // turned it (macOS) hands a horizontal movement, which is left as it is.
        if ((modifiers & KeyModifiers.Shift) != 0 && across == 0)
        {
            across = down;
            down = 0;
        }
        var glide = stepped && !_reduceMotion;
        var fromAcross = glide ? _targetAcross ?? _across : _across;
        var fromDown = glide ? _targetDown ?? _down : _down;
        var toAcross = Math.Clamp(fromAcross + across, 0, MaxAcross);
        var toDown = Math.Clamp(fromDown + down, 0, MaxDown);
        if (glide)
        {
            if (toAcross != fromAcross) GlideAcross(toAcross);
            if (toDown != fromDown) GlideDown(toDown);
        }
        else
        {
            _targetAcross = null;
            _targetDown = null;
            _across = toAcross;
            _down = toDown;
        }
        return new TableOffset(across - (toAcross - fromAcross), down - (toDown - fromDown));
    }

    public bool Pointer(PointerPhase phase, Point position, PointerKind kind, double time)
    {
        switch (phase)
        {
            case PointerPhase.Down:
                return Press(position, kind, time);
            case PointerPhase.Move:
                return Drag(position, time);
            case PointerPhase.Up:
                return Release(time, fling: true);
            case PointerPhase.Cancel:
                return Release(time, fling: false);
            default:
                return false;
        }
    }

    public bool Advance(double time)
    {
        var elapsed = double.IsNaN(_lastTime) ? 0 : Math.Max(0, time - _lastTime);
        _lastTime = time;
        if (_targetAcross is null && _targetDown is null)
        {
            _lastTime = double.NaN;
            return false;
        }
        if (_reduceMotion)
        {
            Land();
            return false;
        }
        // 1 - (1-k)^dt: the same fraction per millisecond however long the frame took.
        var step = 1 - Math.Pow(1 - SmoothingPerMs, elapsed);
        if (_targetAcross is { } targetAcross)
        {
            _across += (targetAcross - _across) * step;
            if (Math.Abs(targetAcross - _across) <= RestingDp)
            {
                _across = targetAcross;
                _targetAcross = null;
            }
        }
        if (_targetDown is { } targetDown)
        {
            _down += (targetDown - _down) * step;
            if (Math.Abs(targetDown - _down) <= RestingDp)
            {
                _down = targetDown;
                _targetDown = null;
            }
        }
        var moving = _targetAcross is not null || _targetDown is not null;
        if (!moving) _lastTime = double.NaN;
        return moving;
    }

    public Rect? TrackOf(ScrollAxis axis)
    {
        if (axis == ScrollAxis.Horizontal)
            return _barAcross ? new Rect(0, _viewport.Height, _viewport.Width, Thickness) : null;
        return _barDown ? new Rect(_viewport.Width, 0, Thickness, _viewport.Height) : null;
    }

    public Rect? ThumbOf(ScrollAxis axis)
    {
        if (TrackOf(axis) is not { } track) return null;
        var horizontal = axis == ScrollAxis.Horizontal;
        var trackLength = horizontal ? track.Width : track.Height;
        // The share of the scrolling part the window shows: the pinned bands never scroll, so they
        // count on neither side.
        var shown = Math.Max(0, (horizontal ? _viewport.Width : _viewport.Height) - (horizontal ? _pinnedAcross : _pinnedDown));
        var content = Math.Max(shown, (horizontal ? _gridAcross - _pinnedAcross : _gridDown - _pinnedDown));
        var length = content <= 0 ? trackLength : trackLength * shown / content;
        length = Math.Min(trackLength, Math.Max(MinThumb, length));
        var max = horizontal ? MaxAcross : MaxDown;
        var travel = max <= 0 ? 0 : (trackLength - length) * ((horizontal ? _across : _down) / max);
        return horizontal
            ? new Rect(track.X + (float)travel, track.Y, (float)length, track.Height)
            : new Rect(track.X, track.Y + (float)travel, track.Width, (float)length);
    }

    public bool Apply(TableScrollRequest? request, TableSpans rows, TableSpans columns, int pinnedRows,
        int pinnedColumns)
    {
        if (request is null || request.Version == _honoured) return false;
        _honoured = request.Version;
        var across = _across;
        var down = _down;
        if (request.Reveal is { } range)
        {
            down = Reveal(down, rows, range.Row, range.Rows, pinnedRows, _pinnedDown, _viewport.Height);
            across = Reveal(across, columns, range.Column, range.Columns, pinnedColumns, _pinnedAcross,
                _viewport.Width);
        }
        else if (request.Offset is { } offset)
        {
            across = offset.Across;
            down = offset.Down;
        }
        across = Math.Clamp(across, 0, MaxAcross);
        down = Math.Clamp(down, 0, MaxDown);
        _targetAcross = null;
        _targetDown = null;
        if (across == _across && down == _down) return false;
        _across = across;
        _down = down;
        return true;
    }

    /// <summary>
    /// The least movement on one axis that brings a run of rows (or columns) inside the part of the
    /// window the pinned band leaves uncovered. A run inside the pinned band is always in view on this
    /// axis; one larger than the uncovered part has its leading edge brought in.
    /// </summary>
    private static double Reveal(double offset, TableSpans spans, int first, int count, int pinned,
        double pinnedExtent, double window)
    {
        if (spans.Count == 0 || first < pinned) return offset;
        var start = spans.OffsetOf(Math.Clamp(first, 0, spans.Count));
        var end = spans.OffsetOf(Math.Clamp(first + Math.Max(1, count), 0, spans.Count));
        if (end - start > window - pinnedExtent || start < offset + pinnedExtent) return start - pinnedExtent;
        if (end > offset + window) return end - window;
        return offset;
    }

    /// <summary>A press: on a scrollbar it grabs the thumb or pages from the track; a finger or a stylus
    /// takes the grid where it stands; a mouse on the cells is the cells'.</summary>
    private bool Press(Point at, PointerKind kind, double time)
    {
        _dragging = null;
        _panning = false;
        foreach (var axis in new[] { ScrollAxis.Vertical, ScrollAxis.Horizontal })
        {
            if (TrackOf(axis) is not { } track || !Contains(track, at)) continue;
            if (ThumbOf(axis) is { } thumb && Contains(thumb, at))
            {
                _dragging = axis;
                _grab = axis == ScrollAxis.Vertical ? at.Y - thumb.Y : at.X - thumb.X;
                return true;
            }
            var thumbStart = ThumbOf(axis) is { } knob ? axis == ScrollAxis.Vertical ? knob.Y : knob.X : 0;
            var back = (axis == ScrollAxis.Vertical ? at.Y : at.X) < thumbStart;
            var page = axis == ScrollAxis.Vertical
                ? _viewport.Height - _pinnedDown
                : _viewport.Width - _pinnedAcross;
            var delta = back ? -page : page;
            Wheel(axis == ScrollAxis.Horizontal ? delta : 0, axis == ScrollAxis.Vertical ? delta : 0, true,
                KeyModifiers.None);
            return true;
        }
        if (kind == PointerKind.Mouse) return false;
        _panning = true;
        _panAt = at;
        _panTime = time;
        _velocityAcross = 0;
        _velocityDown = 0;
        _targetAcross = null;
        _targetDown = null;
        return true;
    }

    /// <summary>A move of the press in flight: the thumb maps along its track onto the grid, a finger
    /// carries the grid with it.</summary>
    private bool Drag(Point at, double time)
    {
        if (_dragging is { } axis)
        {
            if (TrackOf(axis) is not { } track || ThumbOf(axis) is not { } thumb) return false;
            var vertical = axis == ScrollAxis.Vertical;
            var room = vertical ? track.Height - thumb.Height : track.Width - thumb.Width;
            var travel = (vertical ? at.Y - track.Y : at.X - track.X) - _grab;
            var fraction = room <= 0 ? 0 : Math.Clamp(travel / room, 0, 1);
            if (vertical)
            {
                _down = fraction * MaxDown;
                _targetDown = null;
            }
            else
            {
                _across = fraction * MaxAcross;
                _targetAcross = null;
            }
            return true;
        }
        if (!_panning) return false;
        var elapsed = time - _panTime;
        var beforeAcross = _across;
        var beforeDown = _down;
        _across = Math.Clamp(_across - (at.X - _panAt.X), 0, MaxAcross);
        _down = Math.Clamp(_down - (at.Y - _panAt.Y), 0, MaxDown);
        // The grid's speed, leaning on the latest step: what the finger was doing as it let go is what
        // a fling continues. Two moves a host reports at one moment carry no speed between them.
        if (elapsed > 0)
        {
            _velocityAcross = 0.8 * ((_across - beforeAcross) / elapsed) + 0.2 * _velocityAcross;
            _velocityDown = 0.8 * ((_down - beforeDown) / elapsed) + 0.2 * _velocityDown;
            _panTime = time;
        }
        _panAt = at;
        return true;
    }

    /// <summary>The press ends: a finger still moving flings, one that rested or was taken away does not.</summary>
    private bool Release(double time, bool fling)
    {
        if (_dragging is not null)
        {
            _dragging = null;
            return true;
        }
        if (!_panning) return false;
        _panning = false;
        if (!fling || _reduceMotion || time - _panTime > FlingWindow) return true;
        if (Math.Abs(_velocityAcross) >= FlingFloor)
            GlideAcross(Math.Clamp(_across + _velocityAcross * FlingReach, 0, MaxAcross));
        if (Math.Abs(_velocityDown) >= FlingFloor)
            GlideDown(Math.Clamp(_down + _velocityDown * FlingReach, 0, MaxDown));
        return true;
    }

    private void GlideAcross(double target)
    {
        if (_targetAcross is null && _targetDown is null) _lastTime = double.NaN;
        _targetAcross = target;
    }

    private void GlideDown(double target)
    {
        if (_targetAcross is null && _targetDown is null) _lastTime = double.NaN;
        _targetDown = target;
    }

    /// <summary>Whatever a glide was heading for, now.</summary>
    private void Land()
    {
        if (_targetAcross is { } across) _across = across;
        if (_targetDown is { } down) _down = down;
        _targetAcross = null;
        _targetDown = null;
        _lastTime = double.NaN;
    }

    private static bool Contains(Rect box, Point at) =>
        at.X >= box.X && at.X < box.X + box.Width && at.Y >= box.Y && at.Y < box.Y + box.Height;
}
