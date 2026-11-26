namespace eQuantic.UI.Primitives;

/// <summary>
/// Where one pointer's arena is in its life (#1563), as one value rather than a flag per question, so that a
/// later phase (an arena HELD past the pointer's up, for a double tap) is one more value and changes no member's
/// contract.
/// </summary>
internal enum GestureArenaPhase : byte
{
    /// <summary>The pointer's down is still being dispatched: members join, and one that accepts now is the eager
    /// winner, settled when the arena closes.</summary>
    Open = 0,

    /// <summary>The down was dispatched: no member joins any more, and the first member to accept wins at once.</summary>
    Closed = 1,

    /// <summary>A member won, by accepting, by being the last one left or at the pointer's up, or every member
    /// left: the arena says nothing more, and a late resolution is not heard.</summary>
    Decided = 2,
}
