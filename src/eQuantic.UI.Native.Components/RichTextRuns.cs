using eQuantic.UI.Native.Framework;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Native.Components;

/// <summary>
/// Where the pieces of a RICH paragraph sit, and which of them are links.
///
/// <para>
/// Three readers ask, and they must agree or the link breaks in a way nobody sees at once: the
/// POINTER wants a rectangle per fragment (a link that wraps is pressable on both its lines), the
/// KEYBOARD wants ONE stop however many lines it took, and a SCREEN READER wants one announcement
/// with the words in it. Asking each of them to do its own arithmetic is how a link comes to draw
/// in one place and answer in another — the reason <see cref="Shift"/> was already written down as
/// one function, quoted here because this file is now that function's home.
/// </para>
///
/// <para>
/// A linked run has no NODE, which is what made #255 a design question rather than a missing line:
/// a link inside a sentence is a rectangle the layout computed and a string. It gets an identity
/// here — the paragraph's own path with the link's index after a <c>#</c>, a spelling
/// <see cref="Framework.LayoutEngine"/> never produces, so it can collide with neither a position
/// (<c>r/1</c>) nor a key (<c>r/[home]</c>). That path is what a focus stop, a semantic node and
/// the bridge's activate all name, so the three arrive at the same link.
/// </para>
/// </summary>
internal static class RichTextRuns
{
    /// <summary>One link inside a paragraph: what the keyboard stops on and the reader announces.</summary>
    /// <param name="Path">The paragraph's path, then <c>#</c> and the link's index within it.</param>
    /// <param name="Destination">Where following it goes — the only thing the host ever asks.</param>
    /// <param name="Bounds">
    /// The pieces on the link's FIRST line, unioned. A wrapped link has no single rectangle, and the
    /// union of every piece would swallow the words beside it on both lines — a box a reader would
    /// then report as the link. The pointer keeps its per-fragment regions, so nothing is lost where
    /// precision is actually needed.
    /// </param>
    /// <param name="Text">The link's words, joined — its name to a screen reader.</param>
    /// <param name="Rects">
    /// One rectangle per PIECE, which is what the pointer registers: a link that wraps is pressable
    /// on both its lines, and a single box over the two would claim the words beside it.
    /// </param>
    internal readonly record struct Link(
        string Path, string Destination, Rect Bounds, string Text, IReadOnlyList<Rect> Rects);

    /// <summary>
    /// How far a rich piece slides for its LINE's alignment. Asked by the draw, by the pressable
    /// REGION and by the stop, from one function on purpose: a link that moves on screen and not in
    /// the hit test is a link that stops working, and this repo has already shipped a canvas that
    /// drew perfectly and answered no pointer.
    /// </summary>
    internal static float Shift(LayoutNode node, Text text, TextFragment fragment)
    {
        var lines = node.Text?.Lines;
        return lines is not null && fragment.Line < lines.Count
            ? text.Align.Offset(node.Bounds.Width, lines[fragment.Line].Width)
            : 0f;
    }

    /// <summary>One fragment's rectangle in the frame's own coordinates.</summary>
    internal static Rect RectOf(LayoutNode node, Text text, TextFragment fragment) =>
        new(node.Bounds.X + fragment.X + Shift(node, text, fragment),
            node.Bounds.Y + fragment.Y,
            fragment.Width,
            node.Text?.LineHeight ?? node.Bounds.Height);

    /// <summary>
    /// The links in a paragraph, one entry each.
    /// <para>
    /// Adjacent fragments that share a destination are ONE link, because anything between them ends
    /// it: a paragraph naming the same page twice has other words in between, and those words are a
    /// fragment with no destination. That rule needs nothing added to
    /// <see cref="TextFragment"/> — which is a measurement's shape, not an input route's — and it
    /// answers the wrapped case, where one authored run arrives as one fragment per line.
    /// </para>
    /// </summary>
    internal static IReadOnlyList<Link> LinksOf(LayoutNode node, Text text)
    {
        if (node.TextRuns is not { Count: > 0 } fragments) return [];

        // NOTHING is allocated for a paragraph with no linked run, which is nearly every paragraph
        // with runs at all — styled text is common, linked text is not, and this walk is on the
        // per-frame emit path (the semantics walk asks for it too, but only when a platform reader
        // does). The list is born on the first link instead of on the first FRAGMENT.
        List<Link>? links = null;
        var path = node.Path ?? "";
        for (var i = 0; i < fragments.Count;)
        {
            if (fragments[i].Destination is not { Length: > 0 } destination) { i++; continue; }

            var last = i;
            while (last + 1 < fragments.Count && fragments[last + 1].Destination == destination) last++;

            var bounds = RectOf(node, text, fragments[i]);
            var words = fragments[i].Content;
            // Exactly sized: the run of adjacent fragments is already known, and an unwrapped link
            // — one fragment — is the common case a growing list charges two allocations for.
            var rects = new Rect[last - i + 1];
            rects[0] = bounds;
            for (var next = i + 1; next <= last; next++)
            {
                var rect = RectOf(node, text, fragments[next]);
                rects[next - i] = rect;
                words += fragments[next].Content;
                // The FIRST line only — see the Bounds parameter for what unioning all of them costs.
                if (fragments[next].Line == fragments[i].Line) bounds = Union(bounds, rect);
            }

            links ??= [];
            links.Add(new Link($"{path}#{links.Count}", destination, bounds, words, rects));
            i = last + 1;
        }
        return links ?? (IReadOnlyList<Link>)[];
    }

    private static Rect Union(Rect a, Rect b)
    {
        var x = MathF.Min(a.X, b.X);
        var y = MathF.Min(a.Y, b.Y);
        return new Rect(x, y,
            MathF.Max(a.X + a.Width, b.X + b.Width) - x,
            MathF.Max(a.Y + a.Height, b.Y + b.Height) - y);
    }
}
