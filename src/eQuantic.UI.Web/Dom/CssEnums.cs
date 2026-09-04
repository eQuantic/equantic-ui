namespace eQuantic.UI.Web;

public enum Display
{
    Block,
    Inline,
    InlineBlock,
    Flex,
    InlineFlex,
    Grid,
    InlineGrid,
    Contents,
    /// <summary>The legacy flexbox display the multi-line clamp requires (`-webkit-line-clamp`
    /// only applies inside it) — the one place a prefixed value is still the ONLY way.</summary>
    WebkitBox,
    None
}

public enum Position
{
    Static,
    Relative,
    Absolute,
    Fixed,
    Sticky
}

public enum GridFlow
{
    Row,
    Column,
    RowDense,
    ColumnDense
}

public enum FlexDirection
{
    Row,
    Column,
    RowReverse,
    ColumnReverse
}

public enum FlexWrap
{
    NoWrap,
    Wrap,
    WrapReverse
}

public enum AlignItem
{
    Stretch,
    Center,
    FlexStart,
    FlexEnd,
    Baseline,
    Start,
    End
}

public enum JustifyContent
{
    FlexStart,
    FlexEnd,
    Center,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
    Start,
    End,
    Left,
    Right
}

public enum TextAlign
{
    Left,
    Right,
    Center,
    Justify,
    Start,
    End
}
