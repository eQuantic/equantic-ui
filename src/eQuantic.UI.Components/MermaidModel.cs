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

/// <summary>
/// One edge drawn as a CURVE: the box it occupies on the canvas, plus the path that runs through
/// it — the shape mermaid.js draws, which a run of orthogonal boxes can only approximate with
/// corners.
/// <para>
/// The path's numbers are INTEGERS, and that is a parity rule rather than a rounding preference:
/// this string is built by C# for SSR and by the transpiled twin in the browser, and a float
/// formatted through a machine's current culture says <c>72,5</c> in half the world. An integer
/// says the same thing in every language and every locale.
/// </para>
/// <para>
/// The path is stated in the CANVAS's own coordinates and the box carries them as its viewBox
/// origin, so nothing has to be translated into a local frame — the two cannot drift because
/// there is only one frame.
/// </para>
/// </summary>
public sealed class MermaidCurve
{
    public float X { get; set; }
    public float Y { get; set; }
    public float W { get; set; }
    public float H { get; set; }
    public string Path { get; set; } = "";

    /// <summary>The glyph's viewBox — the canvas rectangle this curve lives in, written where the
    /// integers are so that no float ever reaches a string on the drawing side.</summary>
    public string ViewBox { get; set; } = "";
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
    public List<MermaidCurve> Curves { get; set; } = [];
    public List<MermaidArrowhead> Arrows { get; set; } = [];
    public List<MermaidLabel> Labels { get; set; } = [];
}
