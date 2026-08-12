namespace eQuantic.UI.Components;

/// <summary>What one parsed markdown block IS. The set a chat message, a doc page or a release
/// note actually uses — not CommonMark's museum.</summary>
public enum MarkdownBlockKind
{
    Paragraph = 0,
    Heading = 1,
    List = 2,
    Code = 3,
    Quote = 4,
    Table = 5,
    Rule = 6,
}

/// <summary>
/// One styled span inside a line of markdown prose — the unit <see cref="Primitives.TextRun"/>
/// realizes. Flags rather than a hierarchy because emphasis nests (<c>**bold *italic***</c>) and
/// a run simply carries every flag that reached it.
/// </summary>
public sealed class MarkdownRun
{
    public string Text { get; set; } = "";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public bool Code { get; set; }

    /// <summary>Where the run links to; empty means it is plain prose.</summary>
    public string Href { get; set; } = "";

    public bool IsLink => Href.Length > 0;
}

/// <summary>One list entry: its inline runs, how deep it sits, and what its gutter shows —
/// a bullet, or the number the author wrote.</summary>
public sealed class MarkdownListItem
{
    public List<MarkdownRun> Runs { get; set; } = [];

    /// <summary>Nesting depth. The design indents one rung; deeper markdown flattens onto it.</summary>
    public int Depth { get; set; }

    public string Marker { get; set; } = "•";
}

public sealed class MarkdownCell
{
    public List<MarkdownRun> Runs { get; set; } = [];
}

public sealed class MarkdownRow
{
    public List<MarkdownCell> Cells { get; set; } = [];
}

/// <summary>
/// One rendered thing in a markdown document. A flat union rather than a class hierarchy because
/// that is what a document IS — a list of blocks, each with its own shape — and because a flat
/// shape survives serialization (a prefetch payload, a cache) where a polymorphic tree would not.
/// The shape was proven in production by the documentation site this parser was promoted from.
/// </summary>
public sealed class MarkdownBlock
{
    public MarkdownBlockKind Kind { get; set; }

    // Heading
    public int Level { get; set; }
    public string Text { get; set; } = "";

    /// <summary>A slug of the heading, unique within the document — the anchor a table of
    /// contents or a deep link addresses.</summary>
    public string Id { get; set; } = "";

    // Paragraph / Quote — and a heading keeps its runs too, for renderers that want the emphasis.
    public List<MarkdownRun> Runs { get; set; } = [];

    // List
    public List<MarkdownListItem> Items { get; set; } = [];

    // Code
    public string Lang { get; set; } = "";
    public string Raw { get; set; } = "";

    // Table
    public List<MarkdownCell> Head { get; set; } = [];
    public List<MarkdownRow> Rows { get; set; } = [];
}
