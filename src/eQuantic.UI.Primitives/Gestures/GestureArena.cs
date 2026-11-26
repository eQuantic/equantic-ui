namespace eQuantic.UI.Primitives;

/// <summary>
/// Where the members that want a pointer compete for it (#1563): Flutter's <c>GestureArenaManager</c>, written once
/// and run by every target (the browser runs its transpiled twin), so two targets cannot disagree on who wins.
/// <para>
/// Per pointer: the members join while its down is dispatched, in hit order, the innermost first
/// (<see cref="Add"/>), and the arena then closes (<see cref="Close"/>). A member that accepts wins and every other
/// member loses; a member that rejects leaves, and when one member is left it wins; a member alone at the close
/// wins at the close; at the pointer's up, with nothing decided, the FIRST member wins (<see cref="Sweep"/>); and
/// a pointer the system takes away is lost by every member (<see cref="Cancel"/>). Each member hears one outcome
/// per pointer, once. There is no hold yet: an arena is decided at the latest at its pointer's up.
/// </para>
/// </summary>
public sealed class GestureArena
{
    private readonly Dictionary<int, GestureArenaPointer> _arenas = new();

    /// <summary>
    /// Adds <paramref name="member"/> to the arena of <paramref name="pointer"/>, opening one when the pointer has
    /// none, and hands it the entry it resolves the pointer with. A member joins only while the arena is open.
    /// </summary>
    /// <exception cref="InvalidOperationException">The pointer's arena is already closed.</exception>
    public GestureArenaEntry Add(int pointer, IGestureArenaMember member)
    {
        if (!_arenas.TryGetValue(pointer, out var arena))
        {
            arena = new GestureArenaPointer(pointer);
            _arenas[pointer] = arena;
        }
        else if (arena.Phase != GestureArenaPhase.Open)
        {
            throw new InvalidOperationException($"The arena of pointer {pointer} is closed: no member joins it any more.");
        }
        arena.Members.Add(member);
        return new GestureArenaEntry(this, arena, member);
    }

    /// <summary>Whether <paramref name="pointer"/> has an arena that is not decided yet.</summary>
    public bool IsOpen(int pointer) => _arenas.ContainsKey(pointer);

    /// <summary>
    /// The down of <paramref name="pointer"/> was dispatched: no member joins any more. A member alone wins now, and
    /// so does the first member that accepted while the arena was open.
    /// </summary>
    public void Close(int pointer)
    {
        if (!_arenas.TryGetValue(pointer, out var arena) || arena.Phase != GestureArenaPhase.Open) return;
        arena.Phase = GestureArenaPhase.Closed;
        TryToDecide(arena);
    }

    /// <summary>
    /// <paramref name="pointer"/> went up with nothing decided: the first member, the innermost under the down,
    /// wins it, and every other member loses it.
    /// </summary>
    public void Sweep(int pointer)
    {
        if (!_arenas.TryGetValue(pointer, out var arena)) return;
        Decide(arena);
        if (arena.Members.Count == 0) return;
        arena.Members[0].AcceptGesture(pointer);
        for (var i = 1; i < arena.Members.Count; i++) arena.Members[i].RejectGesture(pointer);
    }

    /// <summary>The system took <paramref name="pointer"/> away: every member still competing loses it.</summary>
    public void Cancel(int pointer)
    {
        if (!_arenas.TryGetValue(pointer, out var arena)) return;
        Decide(arena);
        foreach (var member in arena.Members) member.RejectGesture(pointer);
    }

    /// <summary>What an entry asks for: <paramref name="member"/> takes or lets go of the pointer of
    /// <paramref name="arena"/>. An arena already decided, or a member that already left it, hears nothing.</summary>
    internal void Resolve(GestureArenaPointer arena, IGestureArenaMember member, GestureDisposition disposition)
    {
        if (arena.Phase == GestureArenaPhase.Decided) return;
        var at = IndexOf(arena.Members, member);
        if (at < 0) return;
        if (disposition == GestureDisposition.Rejected)
        {
            arena.Members.RemoveAt(at);
            if (ReferenceEquals(arena.EagerWinner, member)) arena.EagerWinner = null;
            member.RejectGesture(arena.Pointer);
            // Only a closed arena decides here: the member's rejection may have decided it already, re-entrantly.
            if (arena.Phase == GestureArenaPhase.Closed) TryToDecide(arena);
            return;
        }
        if (arena.Phase == GestureArenaPhase.Open)
        {
            arena.EagerWinner ??= member;
            return;
        }
        DecideFor(arena, member);
    }

    /// <summary>A closed arena with one member left gives it the pointer; with none it ends; with an eager winner
    /// it gives the pointer to that one.</summary>
    private void TryToDecide(GestureArenaPointer arena)
    {
        if (arena.Members.Count == 0)
        {
            Decide(arena);
            return;
        }
        if (arena.Members.Count == 1)
        {
            Decide(arena);
            arena.Members[0].AcceptGesture(arena.Pointer);
            return;
        }
        if (arena.EagerWinner is { } eager) DecideFor(arena, eager);
    }

    /// <summary><paramref name="winner"/> takes the pointer: every other member loses it first, as in Flutter.</summary>
    private void DecideFor(GestureArenaPointer arena, IGestureArenaMember winner)
    {
        Decide(arena);
        foreach (var member in arena.Members)
            if (!ReferenceEquals(member, winner)) member.RejectGesture(arena.Pointer);
        winner.AcceptGesture(arena.Pointer);
    }

    /// <summary>The arena says nothing more, and its pointer may open a new one.</summary>
    private void Decide(GestureArenaPointer arena)
    {
        arena.Phase = GestureArenaPhase.Decided;
        if (_arenas.TryGetValue(arena.Pointer, out var current) && ReferenceEquals(current, arena)) _arenas.Remove(arena.Pointer);
    }

    /// <summary>Where <paramref name="member"/> is among <paramref name="members"/>, by identity, or -1: a member is
    /// itself whatever its <c>Equals</c> says.</summary>
    private static int IndexOf(List<IGestureArenaMember> members, IGestureArenaMember member)
    {
        for (var i = 0; i < members.Count; i++)
            if (ReferenceEquals(members[i], member)) return i;
        return -1;
    }
}
