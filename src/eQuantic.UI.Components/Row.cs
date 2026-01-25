using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Row - horizontal flex container
/// </summary>
public class Row : Flex
{
    public Row()
    {
        Direction = FlexDirection.Row;
    }
}