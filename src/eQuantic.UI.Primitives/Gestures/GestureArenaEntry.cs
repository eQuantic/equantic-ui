namespace eQuantic.UI.Primitives;

/// <summary>
/// A member's place in one pointer's arena (#1563): Flutter's <c>GestureArenaEntry</c>, what
/// <see cref="GestureArena.Add"/> hands the member it adds, through which the member asks for the pointer or lets
/// it go. Resolving an arena that is already decided is not heard.
/// </summary>
public sealed class GestureArenaEntry
{
    private readonly GestureArena _arena;
    private readonly GestureArenaPointer _pointer;
    private readonly IGestureArenaMember _member;

    internal GestureArenaEntry(GestureArena arena, GestureArenaPointer pointer, IGestureArenaMember member)
    {
        _arena = arena;
        _pointer = pointer;
        _member = member;
    }

    /// <summary>The pointer whose arena this is.</summary>
    public int Pointer => _pointer.Pointer;

    /// <summary>Asks for the pointer (<see cref="GestureDisposition.Accepted"/>) or lets it go
    /// (<see cref="GestureDisposition.Rejected"/>).</summary>
    public void Resolve(GestureDisposition disposition) => _arena.Resolve(_pointer, _member, disposition);
}
