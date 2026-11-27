namespace eQuantic.UI.Primitives;

/// <summary>
/// What the focus does past the last traversable stop of a <see cref="FocusScopeNode"/> (or before its first),
/// going back (#1897): Flutter's <c>TraversalEdgeBehavior</c>. Append-only, like every vocabulary enum: a target
/// bakes the number into its own build.
/// <para>
/// Flutter's <c>leaveFlutterView</c> is not a value. Leaving the page at the outermost edge is the web realizer's
/// fact (<see cref="FocusManager"/> is made with <c>leavesViewAtRoot</c>), not something an app chooses: the word
/// would not exist without a host page.
/// </para>
/// </summary>
public enum TraversalEdgeBehavior : byte
{
    /// <summary>The focus wraps around inside the scope: past the last stop it lands on the first. Flutter's
    /// <c>TraversalEdgeBehavior.closedLoop</c>, the default.</summary>
    ClosedLoop = 0,

    /// <summary>The focus goes on in the enclosing scope, from where this scope is in it. Flutter's
    /// <c>TraversalEdgeBehavior.parentScope</c>.</summary>
    ParentScope = 1,

    /// <summary>The focus stays on the stop it was on. Flutter's <c>TraversalEdgeBehavior.stop</c>.</summary>
    Stop = 2,
}
