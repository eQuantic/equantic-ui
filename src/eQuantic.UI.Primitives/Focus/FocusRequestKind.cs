namespace eQuantic.UI.Primitives;

/// <summary>What a node asked of the <see cref="FocusManager"/> (#1897), the last ask before a write winning.</summary>
internal enum FocusRequestKind : byte
{
    /// <summary>The keyboard, for the node: <see cref="FocusNode.RequestFocus"/>.</summary>
    Focus = 0,

    /// <summary>The keyboard taken away from the node that has it: <see cref="FocusNode.Unfocus"/>.</summary>
    Unfocus = 1,

    /// <summary>One stop on, as Tab goes: <see cref="FocusNode.NextFocus"/>.</summary>
    Next = 2,

    /// <summary>One stop back, as Shift+Tab goes: <see cref="FocusNode.PreviousFocus"/>.</summary>
    Previous = 3,
}
