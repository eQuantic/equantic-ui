namespace eQuantic.UI.Components;

/// <summary>A node reference matched inside a flowchart statement.</summary>
internal sealed class MermaidNodeRef
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public MermaidShape Shape { get; set; }
    public bool Shaped { get; set; }
    public int End { get; set; }
}

/// <summary>An edge operator matched between two node references.</summary>
internal sealed class MermaidEdgeRef
{
    public bool Arrow { get; set; }
    public string Label { get; set; } = "";
    public int End { get; set; }
}

/// <summary>
/// The mermaid subset parser — the v1 the wiki fences: <c>graph/flowchart TD|TB|LR</c> with
/// rect/rounded/circle/diamond nodes, chained <c>--&gt;</c>/<c>---</c> links and <c>|labels|</c>;
/// and <c>sequenceDiagram</c> with participants and <c>-&gt;&gt;</c> messages. Same discipline as
/// <see cref="MarkdownParser"/>: line-oriented, scanner-built, no regex — the whole body crosses
/// eqc, because the browser parses the same source the server parsed.
/// <para>
/// Tolerant by design: a line it does not understand (subgraph, style, click, notes, loops) is
/// SKIPPED, not fatal — a diagram with an unsupported garnish still draws its graph. Only a
/// missing or unknown header returns null, and null is the caller's cue to fall back to showing
/// the fence as code.
/// </para>
/// </summary>
public static class MermaidParser
{
    /// <summary>Parses a mermaid source, or returns null when the grammar is not one this speaks.</summary>
    public static MermaidGraph? Parse(string source)
    {
        if (string.IsNullOrEmpty(source)) return null;
        var lines = source.Replace("\r\n", "\n").Split('\n');

        var graph = HeaderOf(lines);
        if (graph == null) return null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripComment(lines[i]).Trim();
            if (line.Length == 0) continue;
            if (IsHeader(line)) continue;

            if (graph.Kind == MermaidKind.Sequence) ParseSequenceLine(graph, line);
            else ParseFlowchartLine(graph, line);
        }

        if (graph.Kind == MermaidKind.Sequence)
            return graph.Messages.Count == 0 && graph.Nodes.Count == 0 ? null : graph;
        return graph.Nodes.Count == 0 ? null : graph;
    }

    // ---- Header --------------------------------------------------------------------------------

    private static MermaidGraph? HeaderOf(string[] lines)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripComment(lines[i]).Trim();
            if (line.Length == 0) continue;

            if (line.StartsWith("sequenceDiagram", StringComparison.Ordinal))
                return new MermaidGraph { Kind = MermaidKind.Sequence };

            var rest = "";
            if (line.StartsWith("flowchart", StringComparison.Ordinal)) rest = line[9..].Trim();
            else if (line.StartsWith("graph", StringComparison.Ordinal)) rest = line[5..].Trim();
            else return null;

            if (rest == "TD" || rest == "TB") return new MermaidGraph { Vertical = true };
            if (rest == "LR") return new MermaidGraph { Vertical = false };
            return null;
        }
        return null;
    }

    private static bool IsHeader(string line) =>
        line.StartsWith("sequenceDiagram", StringComparison.Ordinal)
        || line.StartsWith("flowchart", StringComparison.Ordinal)
        || line.StartsWith("graph", StringComparison.Ordinal);

    private static string StripComment(string line)
    {
        var at = line.IndexOf("%%", StringComparison.Ordinal);
        return at < 0 ? line : line[..at];
    }

    // ---- Flowchart -------------------------------------------------------------------------

    private static readonly string[] SkipWords =
        ["subgraph", "end", "style", "classDef", "class", "click", "linkStyle", "direction"];

    private static void ParseFlowchartLine(MermaidGraph graph, string line)
    {
        // Whole-word skip: `end` closes a subgraph, but `endpoint --> B` is a statement.
        foreach (var skip in SkipWords)
            if (line == skip || line.StartsWith(skip + " ", StringComparison.Ordinal)) return;

        var text = line.EndsWith(";", StringComparison.Ordinal) ? line[..(line.Length - 1)] : line;

        var from = NodeRefAt(text, 0);
        if (from == null) return;
        Declare(graph, from);

        var pos = from.End;
        var current = from.Id;
        while (true)
        {
            pos = SkipSpaces(text, pos);
            if (pos >= text.Length) break;
            var edge = EdgeRefAt(text, pos);
            if (edge == null) break;
            pos = SkipSpaces(text, edge.End);
            var to = NodeRefAt(text, pos);
            if (to == null) break;
            Declare(graph, to);
            graph.Edges.Add(new MermaidEdge
            {
                From = current,
                To = to.Id,
                Label = edge.Label,
                Arrow = edge.Arrow,
            });
            current = to.Id;
            pos = to.End;
        }
    }

    /// <summary>A declaration WINS over a bare mention: <c>A --&gt; B</c> then <c>B{Ready?}</c>
    /// upgrades B — mermaid lets the shape arrive on any line.</summary>
    private static void Declare(MermaidGraph graph, MermaidNodeRef nodeRef)
    {
        foreach (var known in graph.Nodes)
        {
            if (known.Id != nodeRef.Id) continue;
            if (nodeRef.Shaped)
            {
                known.Label = nodeRef.Label;
                known.Shape = nodeRef.Shape;
            }
            return;
        }
        graph.Nodes.Add(new MermaidNode
        {
            Id = nodeRef.Id,
            Label = nodeRef.Label.Length == 0 ? nodeRef.Id : nodeRef.Label,
            Shape = nodeRef.Shape,
        });
    }

    private static int SkipSpaces(string text, int pos)
    {
        while (pos < text.Length && text[pos] == ' ') pos++;
        return pos;
    }

    private static bool IsIdChar(char c) => char.IsLetter(c) || char.IsDigit(c) || c == '_';

    private static MermaidNodeRef? NodeRefAt(string text, int pos)
    {
        var start = SkipSpaces(text, pos);
        var end = start;
        while (end < text.Length && IsIdChar(text[end])) end++;
        if (end == start) return null;

        var node = new MermaidNodeRef { Id = text[start..end], End = end };
        if (end >= text.Length) return node;

        var c = text[end];
        if (c == '[') return CloseShape(text, end + 1, "]", MermaidShape.Rect, node);
        if (c == '{') return CloseShape(text, end + 1, "}", MermaidShape.Diamond, node);
        if (c == '(')
        {
            if (end + 1 < text.Length && text[end + 1] == '(')
                return CloseShape(text, end + 2, "))", MermaidShape.Circle, node);
            return CloseShape(text, end + 1, ")", MermaidShape.Rounded, node);
        }
        return node;
    }

    private static MermaidNodeRef? CloseShape(string text, int from, string closer,
        MermaidShape shape, MermaidNodeRef node)
    {
        var close = text.IndexOf(closer, from, StringComparison.Ordinal);
        if (close < 0) return null;
        var label = text[from..close].Trim();
        if (label.StartsWith("\"", StringComparison.Ordinal) && label.EndsWith("\"", StringComparison.Ordinal)
            && label.Length >= 2)
            label = label[1..(label.Length - 1)];
        node.Label = label;
        node.Shape = shape;
        node.Shaped = true;
        node.End = close + closer.Length;
        return node;
    }

    /// <summary>Matches <c>--&gt;</c>, <c>---</c>, <c>-.-&gt;</c>, <c>==&gt;</c> and friends, plus an
    /// optional <c>|label|</c> after — dashed and thick draw as the one line style v1 has.</summary>
    private static MermaidEdgeRef? EdgeRefAt(string text, int pos)
    {
        var i = pos;
        var body = 0;
        while (i < text.Length && (text[i] == '-' || text[i] == '=' || text[i] == '.'))
        {
            body++;
            i++;
        }
        if (body < 2) return null;
        var arrow = i < text.Length && text[i] == '>';
        if (arrow) i++;

        var edge = new MermaidEdgeRef { Arrow = arrow, End = i };
        var after = SkipSpaces(text, i);
        if (after < text.Length && text[after] == '|')
        {
            var close = text.IndexOf('|', after + 1);
            if (close > after)
            {
                edge.Label = text[(after + 1)..close].Trim();
                edge.End = close + 1;
            }
        }
        return edge;
    }

    // ---- Sequence ----------------------------------------------------------------------------

    private static readonly string[] MessageArrows = ["-->>", "->>", "-->", "->"];

    private static void ParseSequenceLine(MermaidGraph graph, string line)
    {
        if (line.StartsWith("participant ", StringComparison.Ordinal)
            || line.StartsWith("actor ", StringComparison.Ordinal))
        {
            var rest = line[(line.IndexOf(' ') + 1)..].Trim();
            var alias = rest;
            var display = rest;
            var asAt = rest.IndexOf(" as ", StringComparison.Ordinal);
            if (asAt > 0)
            {
                alias = rest[..asAt].Trim();
                display = rest[(asAt + 4)..].Trim();
            }
            DeclareParticipant(graph, alias, display);
            return;
        }

        var colon = line.IndexOf(':');
        if (colon <= 0) return;
        var head = line[..colon].Trim();
        var label = line[(colon + 1)..].Trim();

        foreach (var arrow in MessageArrows)
        {
            var at = head.IndexOf(arrow, StringComparison.Ordinal);
            if (at <= 0) continue;
            var from = head[..at].Trim();
            var to = head[(at + arrow.Length)..].Trim();
            if (from.Length == 0 || to.Length == 0) return;
            DeclareParticipant(graph, from, from);
            DeclareParticipant(graph, to, to);
            graph.Messages.Add(new MermaidMessage
            {
                From = from,
                To = to,
                Label = label,
                Dashed = arrow.StartsWith("--", StringComparison.Ordinal),
            });
            return;
        }
    }

    private static void DeclareParticipant(MermaidGraph graph, string id, string display)
    {
        foreach (var known in graph.Nodes)
            if (known.Id == id) return;
        graph.Nodes.Add(new MermaidNode { Id = id, Label = display, Shape = MermaidShape.Rect });
    }
}
