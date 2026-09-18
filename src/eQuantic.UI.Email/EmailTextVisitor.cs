using System.Text;
using eQuantic.UI.Primitives;

namespace eQuantic.UI.Email;

/// <summary>
/// The <c>text/plain</c> alternative, walked from the SAME tree as the HTML: one line per text, a
/// blank line where a Column gap separated sections. Writing it by hand is how the two parts drift;
/// walking the tree is how they cannot.
///
/// <para>
/// It shares <see cref="EmailWalk"/>'s refusal set with <see cref="EmailVisitor"/>, and that is
/// NEW: this walk was a switch with no default arm, so a node the medium does not support fell out
/// of it in silence while the HTML threw. In a whole message the difference never showed — the HTML
/// part is built first and would have thrown already — but the two walkers disagreed about what the
/// vocabulary meant, and one of them was wrong. Now neither can be, because there is one answer.
/// </para>
///
/// <para>
/// A sub-walk (a Row's cells, a Link's label) is another instance over its own builder rather than
/// a builder swapped underneath this one: the state a walk writes into is what identifies it, so
/// handing out a second one is cheaper to reason about than mutating the first.
/// </para>
/// </summary>
internal sealed class EmailTextVisitor(IAppTheme theme, StringBuilder text) : EmailWalk
{
    /// <summary>The line break this renderer writes, on every host.
    /// <para>
    /// <c>StringBuilder.AppendLine</c> appends <c>Environment.NewLine</c>, so the plain-text body
    /// came out CRLF on Windows and LF everywhere else — the same message rendered into different
    /// bytes depending on which machine the server happened to be. A mail body is a PAYLOAD, and the
    /// constant an SDK owes its payloads is its own; the transport is what owns the wire format
    /// (SMTP's own CRLF is applied by the client that sends it, not by the tree that built the text).
    /// </para>
    /// </summary>
    private const string Newline = "\n";

    /// <inheritdoc/>
    public override Nothing Visit(UiComponent node, Nothing state)
    {
        using var _ = ComponentBoundary.Enter(node);
        return node.Build(new ComponentContext(theme)).Accept(this, state);
    }

    public override Nothing Visit(Text node, Nothing state)
    {
        if (node.Spans is { Count: > 0 } spans)
        {
            // Run by run, so a LINKED run keeps its address — inline, the convention is
            // "label (URL)", the paragraph-level Link keeps "label: URL".
            var inline = new StringBuilder();
            foreach (var run in spans)
            {
                inline.Append(run.Content);
                if (run.Destination is { } destination) inline.Append($" ({destination})");
            }
            text.Append(inline.ToString()).Append(Newline);
            return state;
        }

        text.Append(node.PlainContent).Append(Newline);
        return state;
    }

    /// <summary>
    /// The gap that separates sections in the HTML separates them here too — a blank line between
    /// children, never after the last, the same rule the spacer rows follow.
    /// </summary>
    public override Nothing Visit(Column node, Nothing state)
    {
        var firstChild = true;
        foreach (var child in node.Children)
        {
            if (!firstChild && node.Gap > 0) text.Append(Newline);
            firstChild = false;
            child.Accept(this, state);
        }
        return state;
    }

    public override Nothing Visit(Row node, Nothing state)
    {
        var parts = new List<string>();
        foreach (var child in node.Children)
        {
            var line = Aside(child).Trim();
            if (line.Length > 0) parts.Add(line);
        }
        if (parts.Count > 0) text.Append(string.Join("  ", parts)).Append(Newline);
        return state;
    }

    public override Nothing Visit(Box node, Nothing state)
    {
        if (node.Child is { } child) child.Accept(this, state);
        return state;
    }

    public override Nothing Visit(Image node, Nothing state)
    {
        if (node.Label.Length > 0) text.Append(node.Label).Append(Newline);
        return state;
    }

    /// <summary>
    /// The address IS the content: a text alternative without the URL is a message the reader
    /// cannot act on. Label first, address after, the convention every plain-text mail has always
    /// used.
    /// </summary>
    public override Nothing Visit(Link node, Nothing state)
    {
        var trimmed = Aside(node.Child).Trim();
        // The same fallback the HTML's aria-label carries: an icon-only link with an empty alt is
        // exactly what Label exists for, and the two alternatives must not drift.
        if (trimmed.Length == 0 && !string.IsNullOrEmpty(node.Label)) trimmed = node.Label;
        text.Append(trimmed.Length > 0 ? $"{trimmed}: {node.Destination}" : node.Destination).Append(Newline);
        return state;
    }

    /// <summary>What a subtree reads as, on its own — a cell of a Row, or a Link's label.</summary>
    private string Aside(VisualNode node)
    {
        var part = new StringBuilder();
        node.Accept(new EmailTextVisitor(theme, part), default);
        return part.ToString();
    }
}
