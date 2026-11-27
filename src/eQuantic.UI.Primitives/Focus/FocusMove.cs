namespace eQuantic.UI.Primitives;

/// <summary>
/// Where the focus goes, as the <see cref="FocusManager"/> answers a walk or a request (#1897): the realizer lands
/// there and reports it back with <see cref="FocusManager.Focused"/>.
/// </summary>
/// <param name="To">The position the focus lands on, or null for no stop: nothing is focused, which is a clear
/// when something is, and for a modal scope with no stop the keyboard held where nothing behind it can take it.</param>
/// <param name="LeavesView">The walk went past the outermost scope of a manager made with
/// <c>leavesViewAtRoot</c>: the focus leaves the page, which only the browser's own Tab can do.</param>
public readonly record struct FocusMove(object? To, bool LeavesView);
