using eQuantic.UI.Core;

namespace eQuantic.UI.Components;

/// <summary>
/// Column - vertical flex container
/// </summary>
public class Column : Flex
{
    public Column()
    {
        Direction = FlexDirection.Column;
    }
}