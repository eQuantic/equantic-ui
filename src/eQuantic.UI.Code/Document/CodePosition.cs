namespace eQuantic.UI.Code;

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
