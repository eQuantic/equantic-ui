namespace eQuantic.UI.Primitives;

/// <summary>
/// A person asked for the menu of a <see cref="ContextTarget"/> (#1531). <see cref="At"/> is where, relative to
/// the target's top-left corner, for a pointer or a finger, and null when the keyboard asked: the space
/// <see cref="Anchored.At"/> takes, so a menu opens where it was asked for with no arithmetic in the app.
/// </summary>
/// <param name="At">The point in the target's box, or null for the keyboard.</param>
public sealed record ContextRequest(Point? At);
