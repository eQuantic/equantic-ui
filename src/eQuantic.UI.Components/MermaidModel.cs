namespace eQuantic.UI.Components;

/// <summary>Which mermaid grammar a source declared.</summary>
public enum MermaidKind
{
    Flowchart = 0,
    Sequence = 1,
}

/// <summary>Node shapes the flowchart subset draws. Each is a component composition, not an SVG
/// document: rect/rounded/circle are Boxes, the decision diamond is a <see cref="Primitives.Vector"/>
/// rhombus — the same single-path vector door icons and packs already use on BOTH targets.</summary>
public enum MermaidShape
{
    Rect = 0,
    Rounded = 1,
    Circle = 2,
    Diamond = 3,
}

public sealed class MermaidNode
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public MermaidShape Shape { get; set; }
}

public sealed class MermaidEdge
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Label { get; set; } = "";

    /// <summary>False for an open link (<c>---</c>): a line with no head.</summary>
    public bool Arrow { get; set; } = true;
}

/// <summary>One sequence-diagram message, in declaration order — the order IS the vertical axis.</summary>
public sealed class MermaidMessage
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Dashed { get; set; }
}

/// <summary>
/// A parsed mermaid source. Flat and serializable like <see cref="MarkdownBlock"/>, and for the
/// same reason: the SAME parser fills it on the server, on Photon and in the browser twin.
/// </summary>
public sealed class MermaidGraph
{
    public MermaidKind Kind { get; set; }

    /// <summary>Flowchart flow axis: true = TD/TB (ranks are rows), false = LR (ranks are columns).</summary>
    public bool Vertical { get; set; } = true;

    public List<MermaidNode> Nodes { get; set; } = [];
    public List<MermaidEdge> Edges { get; set; } = [];
    public List<MermaidMessage> Messages { get; set; } = [];
}

// ---- Solved geometry -------------------------------------------------------------------------
// Whole-dp coordinates by construction (integer sums and exact halves), so the C# layout and its
// transpiled twin place every box on identical numbers — the property the cross-pinned layout
// fixture asserts.

public sealed class MermaidPlacedNode
{
    public MermaidNode Node { get; set; } = new();
    public float X { get; set; }
    public float Y { get; set; }
    public float W { get; set; }
    public float H { get; set; }
}

/// <summary>One orthogonal edge piece — a thin Box on the canvas.</summary>
public sealed class MermaidSegment
{
    public float X { get; set; }
    public float Y { get; set; }
    public float W { get; set; }
    public float H { get; set; }
}

/// <summary>An arrowhead: the tip point and the direction it points (0 down, 1 right, 2 up, 3 left).</summary>
public sealed class MermaidArrowhead
{
    public float X { get; set; }
    public float Y { get; set; }
    public int Direction { get; set; }
}

/// <summary>A small label chip centered on a point — edge labels, message labels.</summary>
public sealed class MermaidLabel
{
    public string Text { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
}

public sealed class MermaidScene
{
    public float Width { get; set; }
    public float Height { get; set; }
    public List<MermaidPlacedNode> Nodes { get; set; } = [];
    public List<MermaidSegment> Segments { get; set; } = [];
    public List<MermaidArrowhead> Arrows { get; set; } = [];
    public List<MermaidLabel> Labels { get; set; } = [];
}
