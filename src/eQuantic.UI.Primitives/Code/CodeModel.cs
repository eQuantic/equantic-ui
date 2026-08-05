namespace eQuantic.UI.Primitives;

/// <summary>
/// A place in a document: which LINE, and how many characters into it. Zero-based, because every
/// other index in the framework is — the gutter is the only place a 1 belongs, and it adds it.
/// <para>
/// A position is a value, not a cursor: it survives being stored, compared and sorted, and an edit
/// produces a new one rather than moving this.
/// </para>
/// </summary>
public readonly record struct CodePosition(int Line, int Column) : IComparable<CodePosition>
{
    public static readonly CodePosition Start = new(0, 0);

    public int CompareTo(CodePosition other) =>
        Line != other.Line ? Line.CompareTo(other.Line) : Column.CompareTo(other.Column);

    public static bool operator <(CodePosition a, CodePosition b) => a.CompareTo(b) < 0;
    public static bool operator >(CodePosition a, CodePosition b) => a.CompareTo(b) > 0;
    public static bool operator <=(CodePosition a, CodePosition b) => a.CompareTo(b) <= 0;
    public static bool operator >=(CodePosition a, CodePosition b) => a.CompareTo(b) >= 0;

    public override string ToString() => $"{Line}:{Column}";
}

/// <summary>
/// A stretch of document between two positions. <see cref="Anchor"/> is where the selection began
/// and <see cref="Focus"/> is where the caret is now — the pair is DIRECTED, because shift+arrow
/// has to know which end it is dragging. <see cref="Start"/>/<see cref="End"/> give the ordered
/// view everything else wants.
/// </summary>
public readonly record struct CodeRange(CodePosition Anchor, CodePosition Focus)
{
    public CodeRange(CodePosition caret) : this(caret, caret) { }

    public CodePosition Start => Anchor <= Focus ? Anchor : Focus;
    public CodePosition End => Anchor <= Focus ? Focus : Anchor;

    /// <summary>A caret rather than a selection — nothing is covered.</summary>
    public bool IsEmpty => Anchor == Focus;

    /// <summary>Collapses to the caret end, the way typing over a selection does.</summary>
    public CodeRange Collapsed() => new(Focus, Focus);
}

/// <summary>What a token MEANS, which is what gives it its colour. Deliberately a small, closed
/// set: a design system has one palette for code, and a language that needs a hundred kinds is
/// asking for a different tool.</summary>
public enum CodeTokenKind : byte
{
    /// <summary>Anything with no special meaning — identifiers, whitespace, unknown text.</summary>
    Plain = 0,
    Keyword = 1,
    /// <summary>Type names: declared types, and the identifiers a language capitalizes by convention.</summary>
    Type = 2,
    String = 3,
    Number = 4,
    Comment = 5,
    /// <summary>Arithmetic, comparison, assignment — the symbols that DO something.</summary>
    Operator = 6,
    /// <summary>Brackets, commas, semicolons — the symbols that only separate.</summary>
    Punctuation = 7,
    /// <summary>An identifier being CALLED or declared as a callable.</summary>
    Function = 8,
    /// <summary>A C# attribute, a decorator, an annotation.</summary>
    Attribute = 9,
    /// <summary>A JSON/XML key or attribute name — the left side of a pair.</summary>
    Property = 10,
    /// <summary>Language constants: true/false/null/None/undefined.</summary>
    Constant = 11,
}

/// <summary>
/// One coloured stretch of ONE line: where it starts, how long it is, what it means. Line-local by
/// design — a tokenizer that answered in document offsets would have to re-run over the whole file
/// whenever a line changed, and an editor changes a line on every keystroke.
/// </summary>
public readonly record struct CodeToken(int Start, int Length, CodeTokenKind Kind)
{
    public int End => Start + Length;
}

/// <summary>
/// A LANGUAGE, as far as colouring is concerned: it reads one line at a time and hands back the
/// spans, carrying an opaque state across the line break.
/// <para>
/// Line-at-a-time with carry-over state is the shape every real editor uses, and the reason is
/// incremental re-colouring: when line 40 changes, only line 40 re-tokenizes — and its successors
/// only if the carried state came out different. A block comment opened on line 40 is exactly that
/// case, and it is why the state exists at all.
/// </para>
/// </summary>
public interface ICodeLanguage
{
    /// <summary>What this language is called, for a picker or a caption.</summary>
    string Name { get; }

    /// <summary>
    /// Appends the tokens of <paramref name="line"/> to <paramref name="into"/> and returns the
    /// state the NEXT line starts in. <paramref name="state"/> is 0 at the top of a document and
    /// otherwise whatever this method last returned — its meaning belongs to the language.
    /// </summary>
    int Tokenize(string line, int state, List<CodeToken> into);

    /// <summary>
    /// How this language COMMENTS, INDENTS and PAIRS — everything the editor's behaviours are
    /// built from. Defaulted, so a language that only wants colours writes nothing.
    /// </summary>
    CodeLanguageRules Rules => CodeLanguageRules.Default;
}
