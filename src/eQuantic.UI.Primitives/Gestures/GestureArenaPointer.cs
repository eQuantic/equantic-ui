namespace eQuantic.UI.Primitives;

/// <summary>
/// One pointer's arena (#1563): its members in the order they joined, the first being the innermost under the
/// down, where it is in its life, and the member that accepted while it was still open.
/// </summary>
internal sealed class GestureArenaPointer
{
    public GestureArenaPointer(int pointer)
    {
        Pointer = pointer;
    }

    /// <summary>The pointer this arena is of.</summary>
    public int Pointer { get; }

    /// <summary>The members still competing, in the order they joined.</summary>
    public List<IGestureArenaMember> Members { get; } = new();

    /// <summary>Where the arena is in its life.</summary>
    public GestureArenaPhase Phase { get; set; } = GestureArenaPhase.Open;

    /// <summary>The first member that accepted while the arena was open, which wins when it closes.</summary>
    public IGestureArenaMember? EagerWinner { get; set; }
}
