using eQuantic.UI.Core;

namespace eQuantic.UI.Components.Layout;

/// <summary>
/// Vertical stack of items with gap
/// </summary>
public class Stack : Flex
{
    public Stack()
    {
        Direction = FlexDirection.Column;
        Gap = "1rem";
    }
}

/// <summary>
/// Vertical stack (explicit alias)
/// </summary>
public class VStack : Stack { }

/// <summary>
/// Horizontal stack of items with gap
/// </summary>
public class HStack : Flex
{
    public HStack()
    {
        Direction = FlexDirection.Row;
        Gap = "1rem";
        Align = AlignItem.Center; // Usually horizontal stacks align center vertically
    }
}
