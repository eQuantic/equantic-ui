namespace eQuantic.UI.Primitives;

/// <summary>
/// A cell of a <see cref="TableViewport"/>: whatever its <see cref="SingleChildNode.Child"/> draws,
/// laid out over the rows and columns it covers, from <see cref="Row"/> and <see cref="Column"/>. Its
/// key is its position, so a cell that stays in view keeps its element while the window moves: the
/// element tree matches a list by key, as Flutter's two-dimensional viewport matches its children by
/// their vicinity.
/// <para>
/// A <see cref="Mark"/> is drawn above every cell instead, over the range it covers, and takes no press:
/// a selection's outline, a copied range's ants, a fill's preview. Marks carry no key of their own,
/// because two of them can start at one cell; a mark that should glide is given one
/// (<see cref="Glide"/>).
/// </para>
/// </summary>
public sealed class TableViewCell : SingleChildNode
{
    public override string NodeKind => "tableViewCell";

    public TableViewCell(int row, int column, VisualNode child, bool mark = false)
        : base(child)
    {
        Row = row;
        Column = column;
        Mark = mark;
        if (!mark) Key = row + ":" + column;
    }

    /// <summary>The first row it covers.</summary>
    public int Row { get; }

    /// <summary>The first column it covers.</summary>
    public int Column { get; }

    /// <summary>How many rows it covers, from <see cref="Row"/>.</summary>
    public int Rows { get; init; } = 1;

    /// <summary>How many columns it covers, from <see cref="Column"/>.</summary>
    public int Columns { get; init; } = 1;

    /// <summary>Drawn above the cells, over its range, and never pressed.</summary>
    public bool Mark { get; }

    /// <summary>
    /// How a mark moves when a later build places the mark of its key (<see cref="VisualNode.Key"/>) over
    /// another range: from the rectangle it stood over to the new one, over the motion's duration and along
    /// its curve (#984), as Flutter's <c>AnimatedPositioned</c> moves its child. Null draws it where it stands
    /// at once, as does a mark the previous build did not hold, and every mark under the platform's
    /// reduced-motion setting. The glide follows the cells, never the scroll: a grid that scrolls carries the
    /// mark with it at once.
    /// </summary>
    public MotionSpec? Glide { get; init; }

    public sealed override TResult Accept<TState, TResult>(
        IVisualNodeVisitor<TState, TResult> visitor, TState state) => visitor.Visit(this, state);
}
