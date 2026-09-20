using eQuantic.UI.Native.Engine;
using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// The box itself: its background, border, shadow and the two paint-only channels a
/// <c>Transition</c> glides (opacity and transform). Every other family draws INSIDE one of these.
/// </summary>
internal sealed partial class EmitVisitor
{
    private void EmitBoxChrome(Box box, EmitState s)
    {
        if (box.Style.Cursor != PointerCursor.Default)
            s.Input.Add(new CursorRegion(s.Node.Bounds, box.Style.Cursor));

        // Spec S3 frosted glass: the backdrop blurs FIRST — under the shadow and the box's
        // own translucent fill (the engine consumes this as a pass split).
        if (box.Style.BackdropBlur > 0)
            s.Builder.BackdropBlur(new RRect(s.Node.Bounds, box.Style.CornerRadius), box.Style.BackdropBlur);

        // Spec S6 — honoured: `BoxStyle.Transition` glides colours, opacity, transform and
        // shadow under the box's own spec (the Anchored/Pinned/Text siblings still snap, and
        // a gradient fill snaps: only solid tokens interpolate). Size glides in layout.
        // Spec S1 FENCE: a gradient's `Via` midpoint is ignored — the shader interpolates two
        // stops, so native paints From→To until the 3-stop paint lands.

        // §05: the analytic shadow draws under the fill (one per node, theme-resolved).
        if (TransitionStore.Only(box.Style.Transition, StyleChannels.Shadow) is { } shadowSpec
            && s.Motion.Transitions is { } st)
        {
            // The elevation's shadow glides as its resolved components, so a card that lifts
            // from level 1 to 3 on hover grows its shadow instead of swapping it — and one
            // that drops to 0 fades it, which "if (Elevation > 0)" alone could never draw.
            // Level 0 is a real spec — zero geometry and a TRANSPARENT colour — so there is
            // no special case to write: asking the theme for the level the box declares
            // gives the right target every time, and the alpha fades with the geometry the
            // way `box-shadow: none` interpolates in CSS.
            var target = s.Theme.Elevation(box.Style.Elevation);
            var sp = (s.Node.Path ?? "") + ":elev";
            var offsetY = st.Resolve(sp + ".y", target.OffsetY, s.Motion.TimeMs, shadowSpec, s.Motion.Reduced);
            var blur = st.Resolve(sp + ".b", target.Blur, s.Motion.TimeMs, shadowSpec, s.Motion.Reduced);
            var spread = st.Resolve(sp + ".s", target.Spread, s.Motion.TimeMs, shadowSpec, s.Motion.Reduced);
            var shadowColor = st.ResolveColor(sp + ".c", target.Color.Resolve(s.Mode),
                s.Motion.TimeMs, shadowSpec, s.Motion.Reduced);
            if (blur > 0 || offsetY != 0 || spread != 0)
            {
                s.Builder.ShadowRRect(new RRect(s.Node.Bounds, box.Style.CornerRadius),
                    offsetY, blur, spread, shadowColor);
            }
        }
        else if (box.Style.Elevation > 0)
        {
            // The plain path, byte-for-byte what it was: no transition, no tracks, no strings.
            var spec = s.Theme.Elevation(box.Style.Elevation);
            if (!spec.IsNone)
            {
                s.Builder.ShadowRRect(new RRect(s.Node.Bounds, box.Style.CornerRadius),
                    spec.OffsetY, spec.Blur, spec.Spread, spec.Color.Resolve(s.Mode));
            }
        }

        // CUSTOM shadow (glows, halos): the same analytic rrect shadow with the caller's
        // full spec — composes with the neutral elevation above. Lists draw in order.
        if (box.Style.Shadow is { } custom)
        {
            s.Builder.ShadowRRect(new RRect(s.Node.Bounds, box.Style.CornerRadius),
                custom.OffsetY, custom.Blur, custom.Spread, custom.Color.Resolve(s.Mode));
        }
        if (box.Style.Shadows is { Count: > 0 } customList)
        {
            foreach (var entry in customList)
                s.Builder.ShadowRRect(new RRect(s.Node.Bounds, box.Style.CornerRadius),
                    entry.OffsetY, entry.Blur, entry.Spread, entry.Color.Resolve(s.Mode));
        }

        // Whether THIS box consumed the pressed swap must be decided before the consume
        // nulls it — checking PendingFill afterwards reads null on exactly the box that
        // took it, and the hover diff below would repaint over the pressed fill (§10:
        // pressed beats hover).
        var pressedHere = s.Press.PendingFill is not null;
        var fill = s.Press.PendingFill ?? box.Style.Background;
        s.Press.PendingFill = null;
        var borderColor = box.Style.BorderColor;
        var borderWidth = box.Style.BorderWidth;
        // Spec S5: hover-reactive boxes register for the host's pointer tracking.
        if (box.Style.Hover is { IsEmpty: false })
            s.Input.Add(new HoverRegion(s.Node.Bounds, box, s.Node.Path ?? ""));

        // Spec S5: the hovered Box applies its Hover diff (pressed still wins on fill).
        // Tracked BY PATH like the press: a component rebuild replaces every instance,
        // and a hover that only knew the old reference would paint exactly one frame.
        if ((s.Press.IsHovered(s.Node, s.Node.Source)
                || (s.Press.Simulated & SimulatedState.Hovered) != 0)
            && box.Style.Hover is { IsEmpty: false } hover)
        {
            if (!pressedHere && hover.Background is { } hoverFill) fill = hoverFill;
            if (hover.BorderColor is { } hoverBorder) borderColor = hoverBorder;
            if (hover.BorderWidth is { } hoverWidth) borderWidth = hoverWidth;
        }
        // Colours glide as resolved sRGB (what CSS does), wrapped back into a token that
        // resolves the same in both modes — the interpolation already picked the mode.
        if (TransitionStore.Only(box.Style.Transition, StyleChannels.Colors) is { } colorSpec
            && s.Motion.Transitions is { } colorStore)
        {
            var cp = s.Node.Path ?? "";
            if (fill is { } solidFill)
                fill = new ColorToken(colorStore.ResolveColor(cp + ":bg", solidFill.Resolve(s.Mode), s.Motion.TimeMs, colorSpec, s.Motion.Reduced));
            borderColor = new ColorToken(colorStore.ResolveColor(cp + ":bd", borderColor.Resolve(s.Mode), s.Motion.TimeMs, colorSpec, s.Motion.Reduced));
        }

        EmitChrome(s.Node.Bounds, fill, box.Style.CornerRadius,
            borderColor, borderWidth, s.Theme, s.Mode, s.Builder,
            box.Style.Gradient, box.Style.Pattern, box.Style.Glow, box.Style.BorderSides);

        // Focus double ring (spec §01): 2dp Surface gap + 2dp FocusRing OUTSIDE the control,
        // following the control's own radius — the first Box under the focused Pressable
        // carries it (the same convention as the pressed fill swap).
        if (s.Press.PendingFocusRing)
        {
            s.Press.PendingFocusRing = false;
            FocusRing(s, s.Node.Bounds, box.Style.CornerRadius);
        }
    }

    /// <summary>
    /// The focus ring of spec §01, from ONE place: 2dp of Surface then 2dp of FocusRing outside the
    /// control, each following its radius. The Box arm draws it for every control that has a box;
    /// a <see cref="Link"/> has none — it is words — and would otherwise leave the ring pending for
    /// whatever box the tree emitted next, which is a ring drawn around the wrong control.
    /// </summary>
    private static void FocusRing(in EmitState s, Rect bounds, CornerRadii radii)
    {
        s.Builder.StrokeRRect(
            new RRect(bounds.Inflate(1), new CornerRadii(
                radii.TopLeft + 1, radii.TopRight + 1, radii.BottomRight + 1, radii.BottomLeft + 1)),
            2, Paint.Solid(s.Theme.Surface.Resolve(s.Mode)));
        s.Builder.StrokeRRect(
            new RRect(bounds.Inflate(3), new CornerRadii(
                radii.TopLeft + 3, radii.TopRight + 3, radii.BottomRight + 3, radii.BottomLeft + 3)),
            2, Paint.Solid(s.Theme.FocusRing.Resolve(s.Mode)));
    }

    private void EmitFlexBackground(FlexNode flex, EmitState s)
    {
        EmitChrome(s.Node.Bounds, flex.Background, flex.CornerRadius, default, 0, s.Theme, s.Mode, s.Builder);
    }

    private static void EmitChrome(Rect bounds, ColorToken? background, CornerRadii radius,
        ColorToken borderColor, float borderWidth, IAppTheme theme, ThemeMode mode, DisplayListBuilder builder,
        LinearGradient? gradient = null, GridPattern? pattern = null, RadialGradient? glow = null,
        BorderSides sides = BorderSides.All)
    {
        if (bounds.IsEmpty) return;

        if (background is { } bg)
        {
            var color = bg.Resolve(mode);
            if (color.A > 0)
                builder.FillRRect(new RRect(bounds, radius), Paint.Solid(color));
        }

        // The grid draws over the solid and UNDER the gradient — the same layer order the web
        // realizer's background-image list produces. Hairlines are ordinary fills bounded by the
        // box: no engine primitive and no shader, which is what keeps this write-once today.
        if (pattern is { } grid && grid.Cell > 0 && grid.LineWidth > 0)
            EmitGridPattern(bounds, grid, mode, builder);

        // The spotlight sits above the grid and below the linear gradient — the same stacking the
        // web realizer's layer list produces (there the FIRST entry is topmost; here the LAST drawn
        // is). The center is a fraction of the box, so it tracks a resize without recomputation.
        if (glow is { } g2)
        {
            builder.FillRRect(new RRect(bounds, radius), Paint.Radial(
                new Point(bounds.X + bounds.Width * g2.CenterX, bounds.Y + bounds.Height * g2.CenterY),
                g2.RadiusX, g2.RadiusY,
                g2.From.Resolve(mode), g2.To.Resolve(mode)));
        }

        // The gradient draws OVER the solid (CSS background-image/background-color composition):
        // Paint.Linear across the box bounds on the declared axis, stops resolved per mode.
        if (gradient is { } g)
        {
            // The axis is a pair of points, so the diagonals need no new engine primitive — only
            // the right corners. ToBottomLeft starts at the TOP-RIGHT corner (CSS's `to bottom left`
            // runs from the opposite corner), which is why the start point is not always the origin.
            var (start, end) = g.Direction switch
            {
                GradientDirection.ToBottom => (
                    new Point(bounds.X, bounds.Y),
                    new Point(bounds.X, bounds.Y + bounds.Height)),
                GradientDirection.ToBottomRight => (
                    new Point(bounds.X, bounds.Y),
                    new Point(bounds.X + bounds.Width, bounds.Y + bounds.Height)),
                GradientDirection.ToBottomLeft => (
                    new Point(bounds.X + bounds.Width, bounds.Y),
                    new Point(bounds.X, bounds.Y + bounds.Height)),
                _ => (
                    new Point(bounds.X, bounds.Y),
                    new Point(bounds.X + bounds.Width, bounds.Y)),
            };
            builder.FillRRect(new RRect(bounds, radius),
                Paint.Linear(start, end, g.From.Resolve(mode), g.To.Resolve(mode)));
        }

        if (borderWidth > 0 && sides != BorderSides.None)
        {
            var color = borderColor.Resolve(mode);
            if (color.A > 0)
            {
                // Borders draw INSIDE the bounds (spec fence). The engine stroke is centered, so a
                // centered stroke on the half-width-deflated shape covers exactly [0, w] inward.
                var stroke = new RRect(bounds.Inflate(-borderWidth / 2), radius.Deflate(borderWidth / 2));

                if (sides == BorderSides.All)
                {
                    builder.StrokeRRect(stroke, borderWidth, Paint.Solid(color));
                }
                else
                {
                    // Only SOME edges: stroke the whole outline, clipped to the band each present
                    // edge occupies. Clipping rather than filling rectangles is what keeps a rounded
                    // box's partial border curving with its corners instead of cutting across them.
                    //
                    // FENCE (stated on BoxStyle.BorderSides): where a present edge meets an absent
                    // one this squares the corner and CSS mitres it. At radius 0 — a section rule,
                    // an accent bar, a table cell, which is what partial borders are for — the two
                    // are identical.
                    EmitBorderEdge(sides, BorderSides.Top, bounds, borderWidth, stroke, color, builder,
                        b => new Rect(b.X, b.Y, b.Width, borderWidth));
                    EmitBorderEdge(sides, BorderSides.Bottom, bounds, borderWidth, stroke, color, builder,
                        b => new Rect(b.X, b.Y + b.Height - borderWidth, b.Width, borderWidth));
                    EmitBorderEdge(sides, BorderSides.Start, bounds, borderWidth, stroke, color, builder,
                        b => new Rect(b.X, b.Y, borderWidth, b.Height));
                    EmitBorderEdge(sides, BorderSides.End, bounds, borderWidth, stroke, color, builder,
                        b => new Rect(b.X + b.Width - borderWidth, b.Y, borderWidth, b.Height));
                }
            }
        }
    }

    private static void EmitBorderEdge(BorderSides sides, BorderSides edge, Rect bounds, float width,
        RRect stroke, Color color, DisplayListBuilder builder, Func<Rect, Rect> band)
    {
        if (!sides.HasFlag(edge)) return;

        builder.PushClip(new RRect(band(bounds)));
        builder.StrokeRRect(stroke, width, Paint.Solid(color));
        builder.PopClip();
    }

    /// <summary>
    /// The <see cref="GridPattern"/> hairlines, matching the CSS layers exactly: rules START at the
    /// box origin and repeat every <c>Cell</c> dp — the same phase as a <c>background-size</c> tile,
    /// so a grid straddling both realizers lands on the same pixels. Lines are clipped to the box by
    /// construction (each rule is sized to the bounds), and a degenerate cell emits nothing rather
    /// than looping forever.
    /// </summary>
    private static void EmitGridPattern(Rect bounds, GridPattern pattern, ThemeMode mode, DisplayListBuilder builder)
    {
        var color = pattern.Color.Resolve(mode);
        if (color.A == 0) return;

        var paint = Paint.Solid(color);
        var line = pattern.LineWidth;

        for (var x = bounds.X; x < bounds.X + bounds.Width; x += pattern.Cell)
            builder.FillRect(new Rect(x, bounds.Y, line, bounds.Height), paint);

        for (var y = bounds.Y; y < bounds.Y + bounds.Height; y += pattern.Cell)
            builder.FillRect(new Rect(bounds.X, y, bounds.Width, line), paint);
    }

    /// <summary>
    /// The identity, spelled through the PRIMARY constructor. <c>new Transform2D()</c> is the record
    /// struct's parameterless constructor and zeroes every field — ScaleX = ScaleY = 0 — which drew
    /// every box that declared no transform at zero size: an empty golden, found by 81 of them.
    /// </summary>
    private static readonly Transform2D IdentityTransform = new(ScaleX: 1, ScaleY: 1);

    /// <summary>The five components of a transform, each its own track — so a rotate-and-scale
    /// hover glides both together under one spec, and removing the transform glides back to identity.</summary>
    private static Transform2D GlideTransform(TransitionStore? store, string path, Transform2D target,
        float timeMs, TransitionSpec? spec, bool reduced)
    {
        if (store is null) return target;
        return new Transform2D(
            store.Resolve(path + ":tx", target.TranslateX, timeMs, spec, reduced),
            store.Resolve(path + ":ty", target.TranslateY, timeMs, spec, reduced),
            store.Resolve(path + ":rot", target.RotationDegrees, timeMs, spec, reduced),
            store.Resolve(path + ":sx", target.ScaleX, timeMs, spec, reduced),
            store.Resolve(path + ":sy", target.ScaleY, timeMs, spec, reduced));
    }

    /// <summary>The CSS transform list twin: translate → rotate → scale, anchored at the box center.</summary>
    private static Matrix2D CenterAnchored(in Transform2D t, Point center) =>
        Matrix2D.Translation(-center.X, -center.Y)
        * Matrix2D.Scale(t.ScaleX, t.ScaleY)
        * Matrix2D.Rotation(t.RotationDegrees * MathF.PI / 180f)
        * Matrix2D.Translation(center.X + t.TranslateX, center.Y + t.TranslateY);
}
