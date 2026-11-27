namespace eQuantic.UI.Primitives;

/// <summary>
/// A place the focus can stand, with the focus nodes it stands under (#1897): what a realizer hands the
/// <see cref="FocusManager"/> for each focusable of its frame, in its own tree order, the way Flutter's
/// <c>WidgetOrderTraversalPolicy</c> reads the focus tree's order and no geometry.
/// </summary>
/// <param name="Position">What identifies the stop's place across builds, as <c>GestureRouter.Recognizers</c> takes
/// its position: the element on Photon, the DOM element on the web.</param>
/// <param name="Chain">The nodes it stands under, outermost first, each as the latest build wrote it. The
/// innermost <c>Focus</c> is the stop's owner; a <see cref="FocusScopeNode"/> in it is a scope the stop is in.
/// Empty for a stop under no <c>Focus</c>.</param>
public readonly record struct TraversalStop(object Position, IReadOnlyList<FocusAttachment> Chain);
