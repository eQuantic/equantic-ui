namespace eQuantic.UI.Primitives;

/// <summary>
/// What a member of a pointer's arena says of the pointer (#1563): Flutter's <c>GestureDisposition</c>. A member
/// that accepts asks for the pointer and wins it, and every other member loses it; a member that rejects lets it
/// go, and when one member is left it wins.
/// </summary>
public enum GestureDisposition : byte
{
    /// <summary>The member asks for the pointer: it is its gesture.</summary>
    Accepted = 0,

    /// <summary>The member lets the pointer go: it is not its gesture.</summary>
    Rejected = 1,
}
