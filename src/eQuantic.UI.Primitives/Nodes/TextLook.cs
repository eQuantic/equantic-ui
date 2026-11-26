namespace eQuantic.UI.Primitives;

/// <summary>
/// The look a <see cref="Text"/> is drawn in once the look of the boxes above it is in (<see cref="Text.Under"/>):
/// what every realizer reads in place of the node's own, optional members (#1577).
/// </summary>
/// <param name="Role">The role of the type scale.</param>
/// <param name="StyleOverride">The exact style over the role's, the node's own or the box's.</param>
/// <param name="MaxLines">The line limit, 0 for none.</param>
/// <param name="Align">The line alignment inside the paragraph.</param>
[ServerOnly]
public readonly record struct TextLook(TypeRole Role, TypeStyle? StyleOverride, int MaxLines, TextAlignment Align);
